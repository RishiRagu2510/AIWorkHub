using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jira.Api.Controllers;
using Jira.Application.Services;
using Jira.Contracts.Issues;
using Jira.Contracts.Timesheets;
using Jira.Infrastructure.Persistence.Timesheets;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using TenantContext;
namespace Jira.Api.Collaboration;
public sealed class IdempotencyFilter(JiraDbContext db,JiraResourceScope scope,ITenantContext actor,TimesheetService timesheets):IAsyncActionFilter,IOrderedFilter
{
    public int Order=>-1000;
    public async Task OnActionExecutionAsync(ActionExecutingContext context,ActionExecutionDelegate next)
    {
        var request=context.HttpContext.Request;
        var eligible=(context.Controller is EmployeeRequestsController && request.Path.Value!.EndsWith("/employee-requests")) || context.Controller is IssuesController or IssueCommentsController or WorkLogsController or TimesheetsController && (HttpMethods.IsPost(request.Method)||HttpMethods.IsPut(request.Method));
        var key=request.Headers["Idempotency-Key"].ToString();
        if(!eligible||string.IsNullOrEmpty(key)){await next();return;}
        if(!actor.IsAuthenticated){context.Result=new UnauthorizedResult();return;}
        if(key.Length>128||key.Any(c=>c<33||c>126)){context.Result=new BadRequestObjectResult(new{code="INVALID_IDEMPOTENCY_KEY"});return;}
        var args=context.ActionArguments.Where(a=>a.Value is not CancellationToken).OrderBy(a=>a.Key).ToDictionary(a=>a.Key,a=>a.Value);
        var permissions=string.Join(',',context.HttpContext.User.Claims.Where(c=>c.Type==TenantClaimTypes.Permission||c.Type==TenantClaimTypes.Role).Select(c=>c.Type+":"+c.Value).Order());
        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(request.Method+request.Path+request.QueryString+request.Headers.IfMatch+permissions+JsonSerializer.Serialize(args))));
        var lockBytes=SHA256.HashData(Encoding.UTF8.GetBytes($"{actor.TenantId}:{actor.UserId}:{key}"));
        var lockId=BitConverter.ToInt32(lockBytes,0);
        await using var tx=await db.Database.BeginTransactionAsync(context.HttpContext.RequestAborted);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({142309},{lockId})",context.HttpContext.RequestAborted);
        var prior=await db.RequestReplays.SingleOrDefaultAsync(r=>r.TenantId==actor.TenantId&&r.UserId==actor.UserId&&r.Key==key,context.HttpContext.RequestAborted);
        if(prior is not null)
        {
            if(prior.Hash!=hash){context.Result=new ConflictObjectResult(new{code="IDEMPOTENCY_KEY_REUSED",message="Use a new key for a different request."});return;}
            var permitted=prior.ResourceType switch {
                "WORKLOG"=>await timesheets.Visible().AnyAsync(w=>w.Id==prior.ResourceId,context.HttpContext.RequestAborted),
                "TIMESHEET"=>await timesheets.VisiblePeriods(context.HttpContext.User.HasClaim(TenantClaimTypes.Permission,"TIMESHEET_TEAM_VIEW")).AnyAsync(p=>p.Id==prior.ResourceId,context.HttpContext.RequestAborted),
                _=>await scope.Issues(actor.TenantId).AnyAsync(i=>i.Id==prior.IssueId,context.HttpContext.RequestAborted)
            };
            if(!permitted){context.Result=new NotFoundResult();return;}
            context.HttpContext.Response.Headers["Idempotency-Replayed"]="true";
            if(prior.ETag is not null)context.HttpContext.Response.Headers.ETag=prior.ETag;
            if(prior.Location is not null)context.HttpContext.Response.Headers.Location=prior.Location;
            context.Result=new ContentResult{Content=prior.Response,ContentType="application/json",StatusCode=prior.Status};return;
        }
        var executed=await next();
        if(executed.Exception is not null&&!executed.ExceptionHandled)return;
        if(executed.Result is not ObjectResult result||(result.StatusCode??200)>=400)return;
        var issueId=result.Value switch {IssueDto issue=>issue.Id,IssueCommentDto comment=>comment.IssueId,WorkLogDto work=>work.IssueId,PeriodDto=>0,_=>0};
        if(issueId==0&&result.Value is not PeriodDto)return;
        string? location=result is CreatedAtActionResult ? result.Value is WorkLogDto w ? $"/api/worklogs/{w.Id}" : $"/api/issues/{issueId}" : null;
        db.RequestReplays.Add(new RequestReplay{TenantId=actor.TenantId,UserId=actor.UserId,Key=key,Hash=hash,
            Status=result.StatusCode??200,Response=JsonSerializer.Serialize(result.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)),
            ResourceType=result.Value is WorkLogDto?"WORKLOG":result.Value is PeriodDto?"TIMESHEET":null,
            ResourceId=result.Value switch{WorkLogDto log=>log.Id,PeriodDto p=>p.Id,_=>(long?)null},IssueId=issueId,ETag=context.HttpContext.Response.Headers.ETag.ToString(),Location=location,CreatedAt=DateTimeOffset.UtcNow});
        await db.SaveChangesAsync(context.HttpContext.RequestAborted);await tx.CommitAsync(context.HttpContext.RequestAborted);
    }
}

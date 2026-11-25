using System.Globalization;
using Jira.Api.Controllers;
using Jira.Application.DTOs;
using Jira.Application.Services;
using Jira.Contracts.Issues;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Jira.Api.Middleware;

public sealed class WritePreconditionFilter(WritePrecondition precondition) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var protectedResource = context.Controller is PlanningController or WorkLogsController or AccountsController or ProjectsController or IssuesController or CommentEditsController;
        var request = context.HttpContext.Request;
        if ((protectedResource && (HttpMethods.IsPut(request.Method) || HttpMethods.IsDelete(request.Method))) || (context.Controller is TimesheetsController && HttpMethods.IsPost(request.Method)) || (context.Controller is PlanningController && HttpMethods.IsPost(request.Method) && (request.Path.Value!.EndsWith("/start") || request.Path.Value.EndsWith("/complete"))))
        {
            var raw = request.Headers.IfMatch.ToString();
            if (string.IsNullOrEmpty(raw))
            {
                context.Result = new ObjectResult(new { code = "VERSION_REQUIRED", message = "Send the resource version as a quoted If-Match header." }) { StatusCode = 428 };
                return;
            }
            if (raw.Length < 3 || raw[0] != '"' || raw[^1] != '"' ||
                !uint.TryParse(raw.AsSpan(1, raw.Length - 2), NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version == 0)
            {
                context.Result = new BadRequestObjectResult(new { code = "INVALID_VERSION", message = "If-Match must contain one quoted resource version." });
                return;
            }
            precondition.ExpectedVersion = version;
        }
        var executed = await next();
        var value = (executed.Result as ObjectResult)?.Value;
        uint? returnedVersion = value switch { ProjectDto project => project.Version, IssueDto issue => issue.Version, Jira.Contracts.Timesheets.WorkLogDto work => work.Version, Jira.Contracts.Timesheets.PeriodDto period => period.Version, _ => null };
        if (returnedVersion.HasValue)
            context.HttpContext.Response.Headers.ETag = $"\"{returnedVersion.Value.ToString(CultureInfo.InvariantCulture)}\"";
    }
}

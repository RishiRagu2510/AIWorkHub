using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Jira.Application.Abstractions.Services;
using Jira.Infrastructure.Persistence;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Authorization.Tests;

public sealed class NotificationsApiTests(SecurityFixture fixture) : IClassFixture<SecurityFixture>
{
    private async Task<HttpResponseMessage> Send(string method, string path, object? body = null,
        long user = 100, long tenant = 1, string role = "ADMIN", string[]? permissions = null, bool anonymous = false)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method),path);
        if (!anonymous) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer",fixture.Token(user,tenant,role,permissions));
        if (body is not null) request.Content = JsonContent.Create(body);
        await VersionedTestRequests.AddCurrentVersionAsync(fixture.Client,request);
        return await fixture.Client.SendAsync(request);
    }
    private async Task<long> Create(long? assignee = 101, long actor = 100)
    {
        using var add = await Send("POST","/api/projects/1/members",new {userId=101});
        Assert.Equal(HttpStatusCode.NoContent,add.StatusCode);
        using var add2 = await Send("POST","/api/projects/1/members",new {userId=102});
        Assert.Equal(HttpStatusCode.NoContent,add2.StatusCode);
        using var response = await Send("POST","/api/issues",new {projectId=1,summary="Notification test",assigneeUserId=assignee},user:actor,role:actor==100?"ADMIN":"DEVELOPER");
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }
    private async Task<JsonElement[]> Inbox(long user = 101, long tenant = 1, string query = "")
    {
        using var response = await Send("GET","/api/notifications?pageSize=100"+query,user:user,tenant:tenant,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToArray();
    }

    [Fact]
    public async Task AssignmentPersistsAndOnlyRecipientCanReadOrMarkIt()
    {
        var issue = await Create();
        var item = Assert.Single(await Inbox(query:"&userId=100&tenantId=2"),n=>n.GetProperty("issueId").GetInt64()==issue);
        Assert.Equal("ISSUE_ASSIGNED",item.GetProperty("kind").GetString());
        Assert.Equal("Notification test",item.GetProperty("issueSummary").GetString());
        var id=item.GetProperty("id").GetGuid();
        foreach(var user in new long[] {100,102})
        {
            Assert.DoesNotContain(await Inbox(user),n=>n.GetProperty("id").GetGuid()==id);
            using var denied=await Send("PUT",$"/api/notifications/{id}/read",user:user);
            Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        }
        using var other=await Send("PUT",$"/api/notifications/{id}/read",user:200,tenant:2);
        Assert.Equal(HttpStatusCode.NotFound,other.StatusCode);
        for(var i=0;i<2;i++)
        {
            using var read=await Send("PUT",$"/api/notifications/{id}/read",user:101,role:"DEVELOPER");
            Assert.Equal(HttpStatusCode.NoContent,read.StatusCode);
        }
        Assert.DoesNotContain(await Inbox(query:"&unreadOnly=true"),n=>n.GetProperty("id").GetGuid()==id);
        Assert.NotEqual(JsonValueKind.Null,Assert.Single(await Inbox(),n=>n.GetProperty("id").GetGuid()==id).GetProperty("readAt").ValueKind);
    }

    [Fact]
    public async Task CommentsNotifyReporterAndAssigneeButNotActorAndSuppressDuplicateRecipients()
    {
        var issue=await Create(102,101);
        using var comment=await Send("POST",$"/api/issues/{issue}/comments",new {body="Private comment"});
        Assert.Equal(HttpStatusCode.OK,comment.StatusCode);
        foreach(var user in new long[] {101,102})
            Assert.Single(await Inbox(user),n=>n.GetProperty("issueId").GetInt64()==issue && n.GetProperty("kind").GetString()=="ISSUE_COMMENT_ADDED");
        Assert.DoesNotContain(await Inbox(100),n=>n.GetProperty("issueId").GetInt64()==issue);
        var self=await Create(101,101);
        Assert.DoesNotContain(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==self);
        using var own=await Send("POST",$"/api/issues/{self}/comments",new {body="Self"},user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,own.StatusCode);
        Assert.DoesNotContain(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==self);
        using var external=await Send("POST",$"/api/issues/{self}/comments",new {body="Other person"});
        Assert.Equal(HttpStatusCode.OK,external.StatusCode);
        Assert.Single(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==self);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BothAssignmentPathsNotifyOnlyOnChangedAssignee(bool generalUpdate)
    {
        var issue=await Create(null);
        var path=generalUpdate?$"/api/issues/{issue}":$"/api/issues/{issue}/assignee";
        for(var i=0;i<2;i++)
        {
            using var response=await Send("PUT",path,new {assigneeUserId=101});
            Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        }
        Assert.Single(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==issue);
    }

    [Fact]
    public async Task ReassignmentAndDeletionHideInaccessibleIssueDetailsAndUnreadCountsMatch()
    {
        var issue=await Create();
        var id=Assert.Single(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==issue).GetProperty("id").GetGuid();
        using var reassigned=await Send("PUT",$"/api/issues/{issue}/assignee",new {assigneeUserId=102});
        Assert.Equal(HttpStatusCode.OK,reassigned.StatusCode);
        Assert.DoesNotContain(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==issue);
        using var denied=await Send("PUT",$"/api/notifications/{id}/read",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.NotFound,denied.StatusCode);
        using var deleted=await Send("DELETE",$"/api/issues/{issue}");
        Assert.Equal(HttpStatusCode.OK,deleted.StatusCode);
        Assert.DoesNotContain(await Inbox(102),n=>n.GetProperty("issueId").GetInt64()==issue);
        using var count=await Send("GET","/api/notifications/unread-count",user:101,role:"DEVELOPER");
        Assert.Equal((await Inbox(query:"&unreadOnly=true")).Length,(await count.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("unreadCount").GetInt32());
    }

    [Fact]
    public async Task ReadAllIsRecipientScopedAndIdempotent()
    {
        var own=await Create();var other=await Create(102);
        using var read=await Send("POST","/api/notifications/read-all",user:101,role:"DEVELOPER");
        Assert.Equal(HttpStatusCode.OK,read.StatusCode);
        Assert.Empty(await Inbox(query:"&unreadOnly=true"));
        Assert.Contains(await Inbox(102,query:"&unreadOnly=true"),n=>n.GetProperty("issueId").GetInt64()==other);
        using var repeated=await Send("POST","/api/notifications/read-all",user:101,role:"DEVELOPER");
        Assert.Equal(0,(await repeated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("updatedCount").GetInt32());
    }

    [Fact]
    public async Task NotificationsRollBackWhenLaterOutboxInsertFails()
    {
        var issue=await Create(null);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION reject_notification_test_event() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.event_type = 'ISSUE_ASSIGNED' THEN RAISE EXCEPTION 'Test failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_notification_test_event BEFORE INSERT ON outbox_messages FOR EACH ROW EXECUTE FUNCTION reject_notification_test_event();
            """);
        try
        {
            using var response=await Send("PUT",$"/api/issues/{issue}/assignee",new {assigneeUserId=101});
            Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
            Assert.False(await db.Notifications.AnyAsync(n=>n.IssueId==issue));
            Assert.Null((await db.Issues.AsNoTracking().SingleAsync(i=>i.Id==issue)).AssigneeUserId);
        }
        finally {await db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_notification_test_event ON outbox_messages; DROP FUNCTION reject_notification_test_event();");}
    }

    [Fact]
    public async Task WriterDeduplicatesRepeatedEventAndMigrationIsRepeatable()
    {
        var issue=await Create(null);
        using var scope=fixture.App.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<JiraDbContext>();
        var sql=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"014_notifications.sql"));
        await db.Database.ExecuteSqlRawAsync(sql); await db.Database.ExecuteSqlRawAsync(sql);
        var entity=await db.Issues.SingleAsync(i=>i.Id==issue); entity.AssignTo(101);
        await using var tx=await db.Database.BeginTransactionAsync();await db.SaveChangesAsync();
        var writer=scope.ServiceProvider.GetRequiredService<INotificationWriter>();var id=Guid.NewGuid();
        await writer.AssignmentAsync(entity,100,id,default);await writer.AssignmentAsync(entity,100,id,default);
        await tx.CommitAsync();
        Assert.Equal(1,await db.Notifications.CountAsync(n=>n.EventId==id));
    }

    [Theory]
    [InlineData("GET","/api/notifications")]
    [InlineData("GET","/api/notifications/unread-count")]
    [InlineData("POST","/api/notifications/read-all")]
    [InlineData("PUT","/api/notifications/00000000-0000-0000-0000-000000000001/read")]
    [InlineData("POST","/hubs/notifications/negotiate?negotiateVersion=1")]
    public async Task EndpointsRequireAuthenticationAndIssueView(string method,string path)
    {
        using var anon=await Send(method,path,anonymous:true);Assert.Equal(HttpStatusCode.Unauthorized,anon.StatusCode);
        using var denied=await Send(method,path,permissions:[]);Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);
    }

    [Fact]
    public async Task HubQueryTokenDoesNotAuthenticateRestEndpoints()
    {
        var token=fixture.Token(101);
        using var rest=await fixture.Client.GetAsync($"/api/notifications?access_token={token}");
        Assert.Equal(HttpStatusCode.Unauthorized,rest.StatusCode);
        using var hub=await fixture.Client.PostAsync($"/hubs/notifications/negotiate?negotiateVersion=1&access_token={token}",null);
        Assert.Equal(HttpStatusCode.OK,hub.StatusCode);
    }

    [Fact]
    public async Task LiveStreamSendsOnlyInvalidationAndReconnectRefreshesOfflineHistory()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var client=fixture.App.GetTestServer().CreateWebSocketClient();
        client.ConfigureRequest=request=>request.Headers.Authorization="Bearer "+fixture.Token(101);
        using var socket=await client.ConnectAsync(new Uri("ws://localhost/hubs/notifications"),timeout.Token);
        await SendFrame(socket,"{\"protocol\":\"json\",\"version\":1}",timeout.Token);
        Assert.Equal("{}",await ReceiveFrame(socket,timeout.Token));
        await SendFrame(socket,"{\"type\":4,\"invocationId\":\"1\",\"target\":\"Watch\",\"arguments\":[]}",timeout.Token);
        AssertInvalidation(await ReceiveFrame(socket,timeout.Token));
        var issue=await Create();
        AssertInvalidation(await ReceiveFrame(socket,timeout.Token));
        Assert.Contains(await Inbox(),n=>n.GetProperty("issueId").GetInt64()==issue);
        socket.Abort();
        await Create(); 
        using var reconnected=await client.ConnectAsync(new Uri("ws://localhost/hubs/notifications"),timeout.Token);
        await SendFrame(reconnected,"{\"protocol\":\"json\",\"version\":1}",timeout.Token);
        Assert.Equal("{}",await ReceiveFrame(reconnected,timeout.Token));
        await SendFrame(reconnected,"{\"type\":4,\"invocationId\":\"2\",\"target\":\"Watch\",\"arguments\":[]}",timeout.Token);
        AssertInvalidation(await ReceiveFrame(reconnected,timeout.Token));
        reconnected.Abort();
    }
    [Fact]
    public async Task LiveStreamDoesNotBroadcastAnotherTenantOrRecipientChanges()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var client=fixture.App.GetTestServer().CreateWebSocketClient();
        client.ConfigureRequest=request=>request.Headers.Authorization="Bearer "+fixture.Token(200,2,"ADMIN");
        using var socket=await client.ConnectAsync(new Uri("ws://localhost/hubs/notifications"),timeout.Token);
        await SendFrame(socket,"{\"protocol\":\"json\",\"version\":1}",timeout.Token);
        Assert.Equal("{}",await ReceiveFrame(socket,timeout.Token));
        await SendFrame(socket,"{\"type\":4,\"invocationId\":\"1\",\"target\":\"Watch\",\"arguments\":[]}",timeout.Token);
        AssertInvalidation(await ReceiveFrame(socket,timeout.Token));
        await Create();
        using var silence=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>ReceiveFrame(socket,silence.Token));
        socket.Abort();
    }

    private static void AssertInvalidation(string frame)
    {
        using var json=JsonDocument.Parse(frame);Assert.Equal(2,json.RootElement.GetProperty("type").GetInt32());
        var item=json.RootElement.GetProperty("item");Assert.True(item.GetProperty("changed").GetBoolean());Assert.All(item.EnumerateObject(),p=>Assert.Contains(p.Name,new[]{"changed","issuesChanged"}));
    }
    private static Task SendFrame(WebSocket socket,string json,CancellationToken ct)
        => socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json+'\u001e')),WebSocketMessageType.Text,true,ct);
    private static async Task<string> ReceiveFrame(WebSocket socket,CancellationToken ct)
    {
        var bytes=new byte[4096];var text=new StringBuilder();
        while(true)
        {
            var result=await socket.ReceiveAsync(new ArraySegment<byte>(bytes),ct);
            Assert.NotEqual(WebSocketMessageType.Close,result.MessageType);
            text.Append(Encoding.UTF8.GetString(bytes,0,result.Count));
            if(text.ToString().Contains('\u001e')) return text.ToString().Split('\u001e')[0];
        }
    }
}

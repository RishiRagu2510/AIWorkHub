using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Jira.Infrastructure.Persistence;
using Jira.Infrastructure.Persistence.Collaboration;
using Messaging.Events;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Backend.Authorization.Tests;
public sealed class CollaborationLiveTests(SecurityFixture fixture):IClassFixture<SecurityFixture>
{
    [Fact]
    public async Task BoardStreamAcceptsItsTenantAndRejectsAnotherTenant()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/boards");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", fixture.Token(100, role: "ADMIN"));
        request.Content = System.Net.Http.Json.JsonContent.Create(new { projectId = 1, name = "Live board", boardType = "SCRUM" });
        using var created = await fixture.Client.SendAsync(request);
        Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);
        using var body = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var boardId = body.RootElement.GetProperty("id").GetInt64();

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        foreach (var tenant in new long[] { 1, 2 })
        {
            var client = fixture.App.GetTestServer().CreateWebSocketClient();
            client.ConfigureRequest = r => r.Headers.Authorization = "Bearer " + fixture.Token(tenant == 1 ? 100 : 200, tenant, "ADMIN");
            using var socket = await client.ConnectAsync(new Uri($"ws://localhost/hubs/boards/{boardId}"), timeout.Token);
            await Send(socket, "{\"protocol\":\"json\",\"version\":1}", timeout.Token);
            await Receive(socket, timeout.Token);
            await Send(socket, $"{{\"type\":4,\"invocationId\":\"board\",\"target\":\"WatchBoard\",\"arguments\":[{boardId}]}}", timeout.Token);
            using var frame = JsonDocument.Parse(await Receive(socket, timeout.Token));
            if (tenant == 1)
                Assert.True(frame.RootElement.GetProperty("item").GetProperty("changed").GetBoolean());
            else
            {
                Assert.Equal(3, frame.RootElement.GetProperty("type").GetInt32());
                Assert.True(frame.RootElement.TryGetProperty("error", out _));
            }
            socket.Abort();
        }
    }

    [Fact]
    public async Task IssueStreamPublishesAuthorizedPresenceAndActivityThenStopsAfterRevocation()
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var client=fixture.App.GetTestServer().CreateWebSocketClient();client.ConfigureRequest=r=>r.Headers.Authorization="Bearer "+fixture.Token(101);
        using var socket=await client.ConnectAsync(new Uri("ws://localhost/hubs/issues"),timeout.Token);
        await Send(socket,"{\"protocol\":\"json\",\"version\":1}",timeout.Token);await Receive(socket,timeout.Token);
        await Send(socket,"{\"type\":1,\"invocationId\":\"pulse\",\"target\":\"Pulse\",\"arguments\":[10,true,true]}",timeout.Token);
        using var pulse=JsonDocument.Parse(await Receive(socket,timeout.Token));Assert.False(pulse.RootElement.TryGetProperty("error",out _));
        await Send(socket,"{\"type\":4,\"invocationId\":\"watch\",\"target\":\"WatchIssue\",\"arguments\":[10]}",timeout.Token);
        using var initial=JsonDocument.Parse(await Receive(socket,timeout.Token));
        Assert.Equal(101,initial.RootElement.GetProperty("item").GetProperty("presence")[0].GetProperty("userId").GetInt64());
        using(var scope=fixture.App.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<ActivityProjection>().ApplyAsync(new IssueAuditEvent{EventId=Guid.NewGuid(),TenantId=1,EntityType="ISSUE",EntityId=10,EntityKey="A-10",EventType="ISSUE_UPDATED",PerformedByUserId=100,OccurredAt=DateTimeOffset.UtcNow},default);
        }
        using var changed=JsonDocument.Parse(await Receive(socket,timeout.Token));Assert.True(changed.RootElement.GetProperty("item").GetProperty("changed").GetBoolean());
        using(var scope=fixture.App.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<JiraDbContext>().Database.ExecuteSqlRawAsync("UPDATE issues SET assignee_user_id=102,reporter_user_id=102 WHERE id=10; DELETE FROM issue_assignees WHERE issue_id=10;");
        }
        using var stopped=JsonDocument.Parse(await Receive(socket,timeout.Token));Assert.Equal(3,stopped.RootElement.GetProperty("type").GetInt32());Assert.True(stopped.RootElement.TryGetProperty("error",out _));
        socket.Abort();
    }
    private static Task Send(WebSocket socket,string value,CancellationToken ct)=>socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(value+'\u001e')),WebSocketMessageType.Text,true,ct);
    private static async Task<string> Receive(WebSocket socket,CancellationToken ct)
    {
        var buffer=new byte[8192];var text=new StringBuilder();
        while(true){var result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),ct);Assert.NotEqual(WebSocketMessageType.Close,result.MessageType);text.Append(Encoding.UTF8.GetString(buffer,0,result.Count));if(text.ToString().Contains('\u001e'))return text.ToString().Split('\u001e')[0];}
    }
}

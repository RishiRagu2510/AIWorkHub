namespace Jira.Application.Tests;





public class ApplicationLayerDependencyTests
{
    [Fact]
    public void TheApplicationLayerDoesNotUseTheBrokerClient()
    {
        var used = typeof(Jira.Application.Services.IssueService).Assembly.GetReferencedAssemblies().Select(a => a.Name);
        Assert.DoesNotContain(used, name => name!.StartsWith("Kafka", StringComparison.Ordinal) || name.StartsWith("Confluent", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Kafka.dll")]
    [InlineData("Confluent.Kafka.dll")]
    public void TheApplicationLayerDoesNotBringTheBrokerClientAlong(string file) =>
        Assert.False(File.Exists(Path.Combine(AppContext.BaseDirectory, file)), $"{file} is copied next to the application layer, so it still depends on the broker.");

    [Fact]
    public void TheMessagesItProducesAreStillAvailableToIt() =>
        Assert.NotNull(typeof(Messaging.Events.IssueAuditEvent));
}

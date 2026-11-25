namespace Jira.Application.Services;



public sealed class WritePrecondition
{
    public uint? ExpectedVersion { get; set; }
    public void Check(uint actualVersion)
    {
        if (ExpectedVersion.HasValue && ExpectedVersion.Value != actualVersion)
            throw new StaleWriteException();
    }
}
public sealed class StaleWriteException : Exception
{
    public StaleWriteException() : base("This resource has changed. Reload it before saving again.") { }
}

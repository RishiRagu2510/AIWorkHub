using System.Diagnostics;
using Xunit;

namespace AIAgent.Application.Tests;





internal static class DockerEngine
{
    public const string SkipReason = "Docker is not running, so this test, which needs a real database in a container, was skipped. Start Docker to run it.";

    private static readonly Lazy<bool> Reachable = new(Probe);

    public static bool IsAvailable => Reachable.Value;

    private static bool Probe()
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("docker", "version --format {{.Server.Version}}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            });
            if (process is null) return false;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            if (process.WaitForExit(TimeSpan.FromSeconds(15))) return process.ExitCode == 0;
            process.Kill(true);
            return false;
        }
        catch (Exception)
        {
            return false;   
        }
    }
}


public sealed class DockerFactAttribute : FactAttribute
{
    public DockerFactAttribute()
    {
        if (!DockerEngine.IsAvailable) Skip = DockerEngine.SkipReason;
    }
}


public sealed class DockerTheoryAttribute : TheoryAttribute
{
    public DockerTheoryAttribute()
    {
        if (!DockerEngine.IsAvailable) Skip = DockerEngine.SkipReason;
    }
}

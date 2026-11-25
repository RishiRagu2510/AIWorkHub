using Authorization;

namespace AIAgent.Application.Interfaces;

public static class ToolLicense
{
    public static bool IsAvailable(IAgentTool tool, IReadOnlyCollection<string>? allowedPortals) =>
        string.IsNullOrWhiteSpace(tool.RequiredPortal) || PortalLicense.IsLicensed(allowedPortals ?? [], tool.RequiredPortal);
}

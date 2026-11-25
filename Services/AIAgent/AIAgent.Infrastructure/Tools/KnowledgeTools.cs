using System.Text.Json;
using AIAgent.Application.Interfaces;

namespace AIAgent.Infrastructure.Tools;






public sealed class SearchCompanyDocsTool(IKnowledgeStore store) : BaseAgentTool
{
    public override string Name => "search_company_docs";
    public override string Category => "General";
    public override string RequiredPermission => "";   

    public override string Description =>
        "Search the company's own documents (policies, handbook, HR and IT guides, how-tos that Admin or HR uploaded) for passages that answer a question about how things work at the company: " +
        "'what is the notice period', 'work from home policy', 'how do I claim travel expenses', 'what is the dress code'. Use it for company rules and process, not for the user's own data, which has its own tools. " +
        "Answer only from the passages returned and name the document you used. If nothing matches, say the company documents do not cover it; never make up a policy.";

    public override object ParameterSchema => new
    {
        type = "OBJECT",
        properties = new Dictionary<string, object>
        {
            ["query"] = new { type = "STRING", description = "The topic or question in a few key words, for example 'notice period resignation' or 'travel expense claim limit'." }
        },
        required = new[] { "query" }
    };

    public override async Task<ToolResult> ExecuteAsync(Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        var query = (Str(arguments, "query") ?? "").Trim();
        if (query.Length == 0) return new ToolResult(false, null, "Tell me what to look for in the company documents.");

        var tenant = Long(arguments, IssueVisibility.CallerTenantIdArgument);
        if (tenant is not > 0) return new ToolResult(false, null, "I can't tell which company you belong to, so I can't search its documents. Please sign in again.");

        var roles = (Str(arguments, IssueVisibility.CallerRolesArgument) ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            var passages = await store.SearchAsync(tenant.Value, roles, KnowledgeRules.CanManage(roles), query, KnowledgeRules.MaxPassages, cancellationToken);
            if (passages.Count == 0)
            {
                return new ToolResult(true, JsonSerializer.Serialize(new
                {
                    found = 0,
                    message = "Nothing in the company documents matches that. Say so plainly and do not guess; suggest asking HR or the document owner."
                }));
            }

            return new ToolResult(true, JsonSerializer.Serialize(new
            {
                found = passages.Count,
                passages = passages.Select(p => new
                {
                    document = p.Title,
                    section = p.Section,
                    updated = p.UpdatedAt.ToString("yyyy-MM-dd"),
                    text = p.Text
                }),
                note = "These are extracts from company documents. Answer only from them, name the document, and if they do not answer the question say so."
            }));
        }
        catch (KnowledgeUnavailableException ex)
        {
            return new ToolResult(false, null, ex.Message);
        }
    }
}

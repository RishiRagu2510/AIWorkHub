using Microsoft.Extensions.Configuration;

namespace AIAgent.Infrastructure.Llm;

public sealed record ProviderStatus(string Id, string Name, bool Available, string? Reason);

public static class ProviderAvailability
{
    public static IReadOnlyList<ProviderStatus> Describe(IConfiguration configuration, OpenAiChatClient openAi, GroqChatClient groq)
    {
        var gemini = !string.IsNullOrWhiteSpace(configuration["AI:ApiKey"] ?? configuration["GEMINI_API_KEY"]);
        return
        [
            new("google", "Google", gemini, gemini ? null : "No Gemini key is configured on the server."),
            new("openai", "OpenAI", openAi.IsConfigured, openAi.IsConfigured ? null : "No OpenAI key is configured on the server (OPENAI_API_KEY)."),
            new("groq", "Groq", groq.IsConfigured, groq.IsConfigured ? null : "No Groq key is configured on the server (GROQ_API_KEY).")
        ];
    }
}

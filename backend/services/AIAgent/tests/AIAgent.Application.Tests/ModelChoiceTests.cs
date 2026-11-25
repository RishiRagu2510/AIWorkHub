using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Contracts.Dtos;
using AIAgent.Domain.Models;
using Xunit;

namespace AIAgent.Application.Tests;

public class ModelChoiceTests
{
    [Theory]
    [InlineData("what are my open issues")]
    [InlineData("Show my leave balance")]
    [InlineData("who is the manager of Asha Kumar")]
    [InlineData("how many leaves do I have left")]
    [InlineData("when is the next holiday")]
    [InlineData("list my projects?")]
    [InlineData("please show my timesheet for today")]
    public void APlainLookup_GoesToTheFastModel(string message)
    {
        Assert.True(ModelChoice.IsSimpleLookup(message));
        Assert.Equal(ModelChoice.Lite, ModelChoice.Resolve(message));
    }

    [Theory]
    [InlineData("create an issue for the login bug")]                      
    [InlineData("log 2 hours on FLP-12")]                                   
    [InlineData("show my issues and my leave balance")]                     
    [InlineData("show my open issues then move the first to done")]         
    [InlineData("what is the salary of Ben")]                               
    [InlineData("show everyone's compensation")]
    [InlineData("export all issues to excel")]                              
    [InlineData("show the report for sprint 4")]
    [InlineData("what about Ben")]                                          
    [InlineData("show me more of those")]
    [InlineData("who is that")]
    [InlineData("why is my leave pending")]                                 
    [InlineData("show issues")]                                             
    [InlineData("what")]
    [InlineData("show my issues. what is my leave balance")]                
    [InlineData("what is in https://example.com/page")]                     
    [InlineData("what are my open issues\nand the overdue ones")]           
    [InlineData("show the issues assigned to me in the current sprint across all of the projects that I work on")]   
    [InlineData("")]
    [InlineData(null)]
    public void AnythingElse_StaysOnTheNormalModel(string? message)
    {
        Assert.False(ModelChoice.IsSimpleLookup(message));
        Assert.Null(ModelChoice.Resolve(message));
    }

    [Theory]
    [InlineData("auto", true)]
    [InlineData(" AUTO ", true)]
    [InlineData("gemini-flash-latest", false)]
    [InlineData("gpt-4.1", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void OnlyTheAutoChoiceIsDecidedForThePerson(string? model, bool auto) =>
        Assert.Equal(auto, ModelChoice.IsAuto(model));

    

    private sealed class RecordingLlm : ILlmProvider
    {
        public List<string?> Models { get; } = [];

        public Task<LlmChatResponse> ChatAsync(List<AgentMessage> messages, IEnumerable<IAgentTool>? availableTools = null, string? model = null, CancellationToken cancellationToken = default)
        {
            Models.Add(model);
            return Task.FromResult(new LlmChatResponse { Content = "ok" });
        }

        public Task<string> GenerateTextAsync(string systemPrompt, string userPrompt, CancellationToken cancellationToken = default) => Task.FromResult("");
    }

    private static async Task<List<string?>> ModelsAsked(string message, string? model)
    {
        var llm = new RecordingLlm();
        var context = new Dictionary<string, object> { ["role"] = "ADMIN" };
        if (model != null) context["model"] = model;
        await new AthennaAgentOrchestrator(llm, []).ExecuteChatAsync(new AgentChatRequest(message, ExtraContext: context));
        return llm.Models;
    }

    [Fact]
    public async Task Auto_SendsAPlainLookupToTheFastModel() =>
        Assert.Equal([ModelChoice.Lite], await ModelsAsked("what are my open issues", "auto"));

    [Fact]
    public async Task Auto_LeavesAnythingElseToTheServersNormalModel() =>
        Assert.Equal([(string?)null], await ModelsAsked("create an issue for the login bug", "auto"));

    [Theory]
    [InlineData("gemini-pro-latest")]
    [InlineData("gemini-flash-latest")]
    [InlineData("gpt-4.1")]
    public async Task AModelThePersonPicked_IsNeverReplaced_EvenForALookup(string model) =>
        Assert.Equal([model], await ModelsAsked("what are my open issues", model));

    [Fact]
    public async Task WithNoChoiceAtAll_TheNormalModelAnswers() =>
        Assert.Equal([(string?)null], await ModelsAsked("what are my open issues", null));
}

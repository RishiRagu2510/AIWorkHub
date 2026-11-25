using AIAgent.Application.Interfaces;
using AIAgent.Application.Services;
using AIAgent.Infrastructure.Attachments;
using AIAgent.Infrastructure.History;
using AIAgent.Infrastructure.Llm;
using AIAgent.Infrastructure.Tools;
using Authentication;

var builder = WebApplication.CreateBuilder(args);


builder.Configuration.AddJsonFile("appsettings.Secrets.json", optional: true, reloadOnChange: false);


builder.Services.AddControllers(options => options.Filters.Add<AIAgent.Api.Licensing.AthennaLicenseFilter>());
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();


builder.Services.AddHttpClient("JiraService", client =>
{
    var baseUri = builder.Configuration["Services:JiraUrl"] ?? "http://localhost:5148";
    client.BaseAddress = new Uri(baseUri);
});

builder.Services.AddHttpClient("IdentityTenantService", client =>
{
    var baseUri = builder.Configuration["Services:IdentityTenantUrl"] ?? "http://localhost:5233";
    client.BaseAddress = new Uri(baseUri);
});



builder.Services.AddHttpClient<LlmProvider>();
builder.Services.AddHttpClient<OpenAiChatClient>();
builder.Services.AddHttpClient<GroqChatClient>();
builder.Services.AddTransient<ILlmProvider, ModelRoutingLlmProvider>();
builder.Services.AddSingleton<IAgentTool, SearchIssuesTool>();
builder.Services.AddSingleton<IAgentTool, GetIssueDetailsTool>();
builder.Services.AddSingleton<IAgentTool, CheckEmployeeLeavesTool>();
builder.Services.AddSingleton<IAgentTool, GetTimesheetSummaryTool>();
builder.Services.AddSingleton<IAgentTool, CreateIssueTool>();
builder.Services.AddSingleton<IAgentTool, LogWorkTool>();
builder.Services.AddSingleton<IAgentTool, PrepareWorkLogTool>();
builder.Services.AddSingleton<IAgentTool, PrepareLeaveTool>();
builder.Services.AddSingleton<IAgentTool, PrepareSkillsTool>();
builder.Services.AddSingleton<IAgentTool, PrepareProfileSectionTool>();
builder.Services.AddSingleton<IAgentTool, ExportIssuesReportTool>();
builder.Services.AddSingleton<IAgentTool, ExportWorkLogReportTool>();
builder.Services.AddSingleton<IAgentTool, PrepareDeleteTool>();
builder.Services.AddSingleton<IAgentTool, GetEmployeeFullDetailsTool>();
builder.Services.AddSingleton<IAgentTool, GetAllEmployeesDetailsTool>();
builder.Services.AddSingleton<IAgentTool, PrepareIssueTool>();
builder.Services.AddSingleton<IAgentTool, AddIssueCommentTool>();
builder.Services.AddSingleton<IAgentTool, TransitionIssueTool>();
builder.Services.AddSingleton<IAgentTool, AssignIssueTool>();
builder.Services.AddSingleton<IAgentTool, UpdateIssueTool>();
builder.Services.AddSingleton<IAgentTool, GetWorkDigestTool>();
builder.Services.AddSingleton<IAgentTool, RememberFactTool>();
builder.Services.AddSingleton<IAgentTool, ListMemoriesTool>();
builder.Services.AddSingleton<IAgentTool, ForgetMemoryTool>();
builder.Services.AddSingleton<IAgentTool, SearchCompanyDocsTool>();
builder.Services.AddSingleton<IAgentTool, GetSprintOverviewTool>();
builder.Services.AddSingleton<IAgentTool, GetCompanyHolidaysTool>();
builder.Services.AddSingleton<IAgentTool, FindPeopleTool>();
builder.Services.AddSingleton<IAgentTool, ListProjectsTool>();
builder.Services.AddSingleton<IAgentTool, ListProjectMembersTool>();
builder.Services.AddSingleton<IAgentTool, ApplyLeaveTool>();
builder.Services.AddSingleton<IAgentTool, ApproveLeaveRequestTool>();
builder.Services.AddSingleton<IAgentTool, GetEmployeeProfileTool>();
builder.Services.AddSingleton<IAgentTool, GetCareerHistoryTool>();
builder.Services.AddSingleton<IAgentTool, GetEmployeeRecordTool>();
builder.Services.AddSingleton<IAgentTool, ExportEmployeeDossierTool>();
builder.Services.AddSingleton<IAgentTool, UpdateMyProfileTool>();
builder.Services.AddSingleton<IAgentTool, GetCompensationMatrixTool>();
builder.Services.AddSingleton<IAgentTool, GetEmployeeCompensationTool>();
builder.Services.AddSingleton<IAgentTool, GetTimesheetHistoryTool>();
builder.Services.AddSingleton<IAgentTool, ApproveTimesheetPeriodTool>();
builder.Services.AddSingleton<IAgentTool, GetWorkLogsTool>();
builder.Services.AddSingleton<IAgentTool, UpdateWorkLogTool>();
builder.Services.AddSingleton<IAgentTool, SubmitTimesheetTool>();


var attachmentKey = builder.Configuration["Attachments:AccessKey"];
if (string.IsNullOrWhiteSpace(attachmentKey) || string.IsNullOrWhiteSpace(builder.Configuration["Attachments:SecretKey"]))
    builder.Services.AddSingleton<IChatAttachmentStore, UnavailableChatAttachmentStore>();
else
{
    builder.Services.AddSingleton(new ChatAttachmentOptions(
        builder.Configuration["Attachments:Endpoint"], attachmentKey, builder.Configuration["Attachments:SecretKey"],
        builder.Configuration["Attachments:Bucket"] ?? "athenna-chat-files", builder.Configuration.GetValue<bool>("Attachments:UseSsl")));
    builder.Services.AddSingleton<IChatAttachmentStore, MinioChatAttachmentStore>();
}


builder.Services.AddScoped<ICallerIdentityProvider, AIAgent.Infrastructure.Identity.JwtCallerIdentityProvider>();
builder.Services.AddAiWorkHubJwtAuthentication(builder.Configuration);
builder.Services.AddAuthorization();


var mongoConnection = builder.Configuration["Mongo:ConnectionString"];
if (string.IsNullOrWhiteSpace(mongoConnection))
{
    builder.Services.AddSingleton<IChatHistoryStore, UnavailableChatHistoryStore>();
    builder.Services.AddSingleton<IUserMemoryStore, AIAgent.Infrastructure.Memory.UnavailableUserMemoryStore>();
    builder.Services.AddSingleton<IAiActionLog, NullAiActionLog>();
}
else
{
    
    builder.Services.AddSingleton(new AIAgent.Infrastructure.Auditing.AiActionLogOptions(
        int.TryParse(builder.Configuration["AiActionLog:RetentionDays"], out var actionDays) && actionDays > 0 ? actionDays : 365));
    builder.Services.AddSingleton<IAiActionLog, AIAgent.Infrastructure.Auditing.MongoAiActionLog>();
    builder.Services.AddSingleton(new ChatHistoryOptions(
        mongoConnection,
        builder.Configuration["Mongo:Database"] ?? "ai_agent_db",
        int.TryParse(builder.Configuration["ChatHistory:RetentionDays"], out var days) && days > 0 ? days : 90));
    builder.Services.AddSingleton<IChatHistoryStore, MongoChatHistoryStore>();
    builder.Services.AddSingleton(new AIAgent.Infrastructure.Memory.UserMemoryOptions(mongoConnection, builder.Configuration["Mongo:Database"] ?? "ai_agent_db"));
    builder.Services.AddSingleton<IUserMemoryStore, AIAgent.Infrastructure.Memory.MongoUserMemoryStore>();
}


if (string.IsNullOrWhiteSpace(builder.Configuration["KnowledgeBase:ConnectionString"]))
    builder.Services.AddSingleton<IKnowledgeStore, AIAgent.Infrastructure.Knowledge.UnavailableKnowledgeStore>();
else
{
    builder.Services.Configure<AIAgent.Infrastructure.Knowledge.KnowledgeBaseOptions>(builder.Configuration.GetSection(AIAgent.Infrastructure.Knowledge.KnowledgeBaseOptions.Section));
    
    builder.Services.AddHttpClient("KnowledgeEmbeddings");
    builder.Services.AddSingleton<IKnowledgeEmbedder>(sp => new AIAgent.Infrastructure.Knowledge.GeminiKnowledgeEmbedder(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("KnowledgeEmbeddings"),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<AIAgent.Infrastructure.Knowledge.GeminiKnowledgeEmbedder>>()));
    builder.Services.AddSingleton<AIAgent.Infrastructure.Knowledge.PostgresKnowledgeStore>();
    builder.Services.AddSingleton<IKnowledgeStore>(sp => sp.GetRequiredService<AIAgent.Infrastructure.Knowledge.PostgresKnowledgeStore>());
    builder.Services.AddSingleton<IKnowledgeIndexer>(sp => sp.GetRequiredService<AIAgent.Infrastructure.Knowledge.PostgresKnowledgeStore>());
    builder.Services.AddHostedService<AIAgent.Infrastructure.Knowledge.KnowledgeEmbeddingWorker>();
}


builder.Services.AddScoped<IAthennaAgentOrchestrator, AthennaAgentOrchestrator>();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<NudgeService>(sp => new NudgeService(sp.GetServices<IAgentTool>(), sp.GetRequiredService<ICallerIdentityProvider>(), sp.GetRequiredService<Microsoft.Extensions.Caching.Memory.IMemoryCache>()));


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowAll");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

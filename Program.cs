using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OllamaToolApproval.Approvals;
using OllamaToolApproval.Chat;
using OllamaToolApproval.Shop;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ApprovalOptions>(builder.Configuration.GetSection("Approvals"));
builder.Services.AddSingleton<ShopStore>();
builder.Services.AddSingleton<ShopTools>();
builder.Services.AddSingleton<IToolApprovalPolicy, RefundOrderPolicy>();
builder.Services.AddSingleton<IToolApprovalPolicy, DeleteCustomerPolicy>();
builder.Services.AddSingleton<ToolApprovalGate>();
builder.Services.AddSingleton<ApprovalBroker>();
builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddSingleton<ChatTurnRunner>();

// Default HttpClient timeout and no model preloading; see Part 2 for production settings.
builder.Services.AddSingleton<IChatClient>(_ => new OllamaApiClient(
    new Uri(builder.Configuration["Ollama:Endpoint"] ?? "http://localhost:11434"),
    builder.Configuration["Ollama:Model"] ?? "qwen3:8b"));

builder.Services.AddSingleton<AIAgent>(sp => sp.GetRequiredService<IChatClient>().AsAIAgent(
    new ChatClientAgentOptions
    {
        Name = "GozoPantrySupport",
        ChatOptions = new()
        {
            Instructions = AgentInstructions.Text,
            Tools = [.. sp.GetRequiredService<ShopTools>().All],
            // qwen3 thinks by default; turning it off keeps tool round-trips fast on a laptop GPU.
            Reasoning = new() { Effort = ReasoningEffort.None },
        },
    },
    sp.GetRequiredService<ILoggerFactory>(),
    sp));

var app = builder.Build();

app.UseOwnerCookie();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/chat", (ChatRequest request, HttpContext http, ChatTurnRunner runner) =>
    string.IsNullOrWhiteSpace(request.Message)
        ? Results.BadRequest("Message is required.")
        : TypedResults.ServerSentEvents(runner.RunAsync(http.GetOwnerId(), request.Message, http.RequestAborted)));

app.MapPost("/approvals/{id:guid}", (Guid id, ApprovalReply reply, HttpContext http, ApprovalBroker broker) =>
    broker.TryResolve(id, http.GetOwnerId(), reply.Approved) switch
    {
        ResolveResult.Resolved => Results.NoContent(),
        ResolveResult.AlreadyResolved => Results.Conflict("This approval was already answered."),
        _ => Results.NotFound(),
    });

app.Run();

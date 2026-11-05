using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OllamaToolApproval.Approvals;

namespace OllamaToolApproval.Chat;

/// <summary>Runs one user turn as a single SSE stream, pausing in place whenever a tool call needs a human.</summary>
public sealed class ChatTurnRunner(
    AIAgent agent,
    ConversationStore conversations,
    ToolApprovalGate gate,
    ApprovalBroker broker,
    ILogger<ChatTurnRunner> logger)
{
    public async IAsyncEnumerable<SseItem<object>> RunAsync(
        string ownerId, string userMessage, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetAsync(ownerId);
        await conversation.TurnLock.WaitAsync(cancellationToken);
        try
        {
            List<ChatMessage> input = [new(ChatRole.User, userMessage)];
            HashSet<string> rejectedCallIds = [];
            Dictionary<string, string> toolNamesByCallId = [];

            while (input.Count > 0)
            {
                List<ToolApprovalRequestContent> requests = [];

                await foreach (var update in agent.RunStreamingAsync(input, conversation.Session, cancellationToken: cancellationToken))
                {
                    foreach (var content in update.Contents)
                    {
                        switch (content)
                        {
                            case TextContent { Text.Length: > 0 } text:
                                yield return ChatEvents.Text(text.Text);
                                break;
                            case ToolApprovalRequestContent request:
                                requests.Add(request);
                                break;
                            case FunctionCallContent call:
                                toolNamesByCallId[call.CallId] = call.Name;
                                break;
                            case FunctionResultContent result when !rejectedCallIds.Contains(result.CallId):
                                yield return ChatEvents.ToolStatus(toolNamesByCallId.GetValueOrDefault(result.CallId, "tool"), "executed");
                                break;
                        }
                    }
                }

                List<AIContent> responses = [];
                foreach (var request in requests)
                {
                    if (request.ToolCall is not FunctionCallContent call)
                    {
                        responses.Add(request.CreateResponse(false, "Unsupported tool call."));
                        continue;
                    }

                    toolNamesByCallId[call.CallId] = call.Name;
                    var decision = await gate.EvaluateAsync(call, cancellationToken);
                    logger.LogInformation("Tool {Tool} ({CallId}) for {Owner}: {Decision}", call.Name, call.CallId, ownerId, decision);

                    switch (decision)
                    {
                        case AutoApprove:
                            yield return ChatEvents.ToolStatus(call.Name, "auto_approved");
                            responses.Add(request.CreateResponse(true, "Auto-approved by policy."));
                            break;

                        case AutoReject reject:
                            rejectedCallIds.Add(call.CallId);
                            yield return ChatEvents.ToolStatus(call.Name, "auto_rejected", reject.Reason);
                            responses.Add(request.CreateResponse(false, reject.Reason));
                            break;

                        case AskUser ask:
                            var pending = broker.Open(ownerId);
                            yield return ChatEvents.ApprovalRequired(new(
                                pending.Id, call.Name, ask.Message, ask.Destructive, call.Arguments, (int)broker.Timeout.TotalSeconds));

                            var outcome = await broker.WaitAsync(pending, cancellationToken);
                            logger.LogInformation("Approval {ApprovalId} for {Tool}: {Outcome}", pending.Id, call.Name, outcome);

                            var approved = outcome == ApprovalOutcome.Approved;
                            if (!approved) rejectedCallIds.Add(call.CallId);
                            yield return ChatEvents.ToolStatus(call.Name, outcome switch
                            {
                                ApprovalOutcome.Approved => "approved",
                                ApprovalOutcome.Denied => "denied",
                                _ => "timed_out",
                            });
                            responses.Add(request.CreateResponse(approved, outcome switch
                            {
                                ApprovalOutcome.Approved => "Approved by the user.",
                                ApprovalOutcome.Denied => "The user denied this action.",
                                _ => "Nobody approved this action in time, so it was not performed.",
                            }));
                            break;
                    }
                }

                input = responses.Count > 0 ? [new ChatMessage(ChatRole.User, responses)] : [];
            }

            yield return ChatEvents.Done();
        }
        finally
        {
            conversation.TurnLock.Release();
        }
    }
}

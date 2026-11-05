using System.Net.ServerSentEvents;

namespace OllamaToolApproval.Chat;

public sealed record ChatRequest(string Message);

public sealed record ApprovalReply(bool Approved);

public sealed record TextEvent(string Text);

public sealed record ApprovalRequiredEvent(
    Guid Id, string Tool, string Message, bool Destructive, IDictionary<string, object?>? Arguments, int TimeoutSeconds);

public sealed record ToolStatusEvent(string Tool, string Status, string? Detail = null);

public sealed record DoneEvent;

public static class ChatEvents
{
    public static SseItem<object> Text(string text) => new(new TextEvent(text), "text");

    public static SseItem<object> ApprovalRequired(ApprovalRequiredEvent e) => new(e, "approval_required");

    public static SseItem<object> ToolStatus(string tool, string status, string? detail = null) =>
        new(new ToolStatusEvent(tool, status, detail), "tool_status");

    public static SseItem<object> Done() => new(new DoneEvent(), "done");
}

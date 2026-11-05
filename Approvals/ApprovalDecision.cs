using Microsoft.Extensions.AI;

namespace OllamaToolApproval.Approvals;

public abstract record ApprovalDecision;

public sealed record AutoApprove : ApprovalDecision;

public sealed record AutoReject(string Reason) : ApprovalDecision;

public sealed record AskUser(string Message, bool Destructive = false) : ApprovalDecision;

/// <summary>Runs before a human sees anything: rejects invalid calls, approves trivial ones, and words the rest.</summary>
public interface IToolApprovalPolicy
{
    string ToolName { get; }

    ValueTask<ApprovalDecision> EvaluateAsync(FunctionCallContent call, CancellationToken cancellationToken);
}

public sealed class ApprovalOptions
{
    public int TimeoutSeconds { get; set; } = 120;
    public int AutoApproveRefundCents { get; set; } = 500;
}

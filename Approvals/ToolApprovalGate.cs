using System.Collections.Frozen;
using Microsoft.Extensions.AI;
using OllamaToolApproval.Shop;

namespace OllamaToolApproval.Approvals;

/// <summary>Picks the policy for a tool call. Approval-required tools without a policy are denied (fail closed).</summary>
public sealed class ToolApprovalGate
{
    private readonly FrozenDictionary<string, IToolApprovalPolicy> _policies;
    private readonly FrozenSet<string> _noApprovalTools;

    public ToolApprovalGate(IEnumerable<IToolApprovalPolicy> policies, ShopTools tools)
    {
        _policies = policies.ToFrozenDictionary(p => p.ToolName, StringComparer.Ordinal);
        _noApprovalTools = tools.All.Where(t => t is not ApprovalRequiredAIFunction).Select(t => t.Name).ToFrozenSet(StringComparer.Ordinal);
    }

    public ValueTask<ApprovalDecision> EvaluateAsync(FunctionCallContent call, CancellationToken cancellationToken)
    {
        if (_policies.TryGetValue(call.Name, out var policy))
            return policy.EvaluateAsync(call, cancellationToken);

        // When one call in a batch needs approval, the framework turns every call in that batch into an approval request.
        if (_noApprovalTools.Contains(call.Name))
            return ValueTask.FromResult<ApprovalDecision>(new AutoApprove());

        return ValueTask.FromResult<ApprovalDecision>(new AutoReject($"No approval policy is registered for '{call.Name}'."));
    }
}

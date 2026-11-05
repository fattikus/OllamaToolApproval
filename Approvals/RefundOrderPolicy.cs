using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OllamaToolApproval.Shop;

namespace OllamaToolApproval.Approvals;

public sealed class RefundOrderPolicy(ShopStore store, IOptions<ApprovalOptions> options) : IToolApprovalPolicy
{
    public string ToolName => "refund_order";

    public ValueTask<ApprovalDecision> EvaluateAsync(FunctionCallContent call, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Evaluate(call));

    private ApprovalDecision Evaluate(FunctionCallContent call)
    {
        if (!call.TryGetGuid("orderId", out var orderId) || store.FindOrder(orderId) is not { } order)
            return new AutoReject("That order doesn't exist. Look the order up by its number first.");

        if (!call.TryGetInt("amountCents", out var amountCents) || amountCents <= 0)
            return new AutoReject("The refund amount must be a positive number of euro cents.");

        if (order.RefundableCents == 0)
            return new AutoReject($"Order #{order.Number} has already been refunded in full ({Money.Euros(order.RefundedCents)}).");

        if (amountCents > order.RefundableCents)
            return new AutoReject($"{Money.Euros(amountCents)} is more than the {Money.Euros(order.RefundableCents)} still refundable on order #{order.Number}.");

        if (amountCents <= options.Value.AutoApproveRefundCents)
            return new AutoApprove();

        var customer = store.FindCustomer(order.CustomerId)?.Name ?? "a deleted customer";
        var scope = amountCents == order.TotalCents ? "full refund" : $"order total {Money.Euros(order.TotalCents)}";
        return new AskUser($"Refund **{Money.Euros(amountCents)}** to **{customer}** for order #{order.Number} ({order.Summary}, {scope})?");
    }
}

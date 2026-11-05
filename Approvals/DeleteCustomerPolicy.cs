using System.Globalization;
using Microsoft.Extensions.AI;
using OllamaToolApproval.Shop;

namespace OllamaToolApproval.Approvals;

public sealed class DeleteCustomerPolicy(ShopStore store) : IToolApprovalPolicy
{
    public string ToolName => "delete_customer";

    public ValueTask<ApprovalDecision> EvaluateAsync(FunctionCallContent call, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Evaluate(call));

    private ApprovalDecision Evaluate(FunctionCallContent call)
    {
        if (!call.TryGetGuid("customerId", out var customerId) || store.FindCustomer(customerId) is not { } customer)
            return new AutoReject("That customer doesn't exist.");

        var orders = store.OrdersFor(customer.Id);
        var open = orders.Where(o => o.Status == OrderStatus.Processing).ToList();
        if (open.Count > 0)
        {
            var numbers = string.Join(", ", open.Select(o => $"#{o.Number}"));
            return new AutoReject($"{customer.Name} has {open.Count} open order(s) ({numbers}). Close or cancel them before deleting the account.");
        }

        var history = orders.Count == 0
            ? "no past orders"
            : $"{orders.Count} past order(s), last order {orders[^1].PlacedOn.ToString("MMM yyyy", CultureInfo.InvariantCulture)}";

        // Never auto-approve: deleting an account can't be undone.
        return new AskUser($"Permanently delete **{customer.Name}** ({history})? This can't be undone.", Destructive: true);
    }
}

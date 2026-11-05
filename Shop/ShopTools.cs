using System.ComponentModel;
using Microsoft.Extensions.AI;

namespace OllamaToolApproval.Shop;

public sealed class ShopTools
{
    private readonly ShopStore _store;

    public ShopTools(ShopStore store)
    {
        _store = store;
        All =
        [
            AIFunctionFactory.Create(LookupOrder, "lookup_order",
                "Looks up an order by its order number (e.g. 1042). Returns the order id, customer id and name, items, delivery status and total in euro cents."),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(RefundOrder, "refund_order",
                "Refunds part or all of an order. Requires the order id (GUID) from lookup_order and the amount in euro cents.")),
            new ApprovalRequiredAIFunction(AIFunctionFactory.Create(DeleteCustomer, "delete_customer",
                "Permanently deletes a customer account. Requires the customer id (GUID) from lookup_order.")),
        ];
    }

    public IReadOnlyList<AITool> All { get; }

    // Fulfilment view only: refunds live in the payments ledger, which the model never sees but the policy does.
    private object LookupOrder([Description("The order number, e.g. 1042")] int orderNumber)
    {
        var order = _store.FindOrderByNumber(orderNumber);
        if (order is null) return $"No order #{orderNumber} exists.";

        var customer = _store.FindCustomer(order.CustomerId);
        return new
        {
            orderId = order.Id,
            orderNumber = order.Number,
            customerId = order.CustomerId,
            customerName = customer?.Name ?? "(deleted customer)",
            placedOn = order.PlacedOn,
            status = order.Status.ToString(),
            items = order.Summary,
            totalCents = order.TotalCents,
        };
    }

    // The approval policy already checked this call; the tool re-validates because the policy is UX, not authorization.
    private string RefundOrder(
        [Description("The order id (GUID) returned by lookup_order")] Guid orderId,
        [Description("Amount to refund in euro cents, e.g. 4250 for €42.50")] int amountCents) =>
        _store.Refund(orderId, amountCents) switch
        {
            RefundResult.Refunded => $"Refunded {Money.Euros(amountCents)}.",
            RefundResult.OrderNotFound => "Refund failed: order not found.",
            RefundResult.NothingLeftToRefund => "Refund failed: the order has already been fully refunded.",
            RefundResult.AmountTooLarge => "Refund failed: amount is larger than the refundable balance.",
            _ => "Refund failed: amount must be positive.",
        };

    private string DeleteCustomer([Description("The customer id (GUID) returned by lookup_order")] Guid customerId) =>
        _store.DeleteCustomer(customerId) switch
        {
            DeleteResult.Deleted => "Customer deleted.",
            DeleteResult.HasOpenOrders => "Delete failed: the customer has open orders.",
            _ => "Delete failed: customer not found.",
        };
}

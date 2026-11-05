using System.Globalization;

namespace OllamaToolApproval.Shop;

public static class Money
{
    public static string Euros(int cents) => string.Create(CultureInfo.InvariantCulture, $"€{cents / 100m:0.00}");
}

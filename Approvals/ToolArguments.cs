using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace OllamaToolApproval.Approvals;

/// <summary>Reads tool-call arguments whether the provider hands them over as JsonElement, string or number.</summary>
public static class ToolArguments
{
    public static bool TryGetGuid(this FunctionCallContent call, string name, out Guid value)
    {
        value = default;
        return TryGetText(call, name, out var text) && Guid.TryParse(text, out value);
    }

    public static bool TryGetInt(this FunctionCallContent call, string name, out int value)
    {
        value = default;
        return TryGetText(call, name, out var text) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryGetText(FunctionCallContent call, string name, out string? text)
    {
        text = null;
        if (call.Arguments?.TryGetValue(name, out var raw) != true) return false;

        text = raw switch
        {
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString(),
            JsonElement { ValueKind: JsonValueKind.Number } e => e.GetRawText(),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            string s => s,
            _ => null,
        };
        return text is not null;
    }
}

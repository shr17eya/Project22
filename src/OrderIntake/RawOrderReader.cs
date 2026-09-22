using System.Text.Json;

namespace OrderIntake;

/// <summary>
/// Turns a JSON string into a <see cref="RawOrder"/>, or reports failure for
/// anything that counts as "malformed input": invalid JSON, a non-object
/// root, or a recognized field whose JSON type doesn't match what's expected
/// (e.g. {"orderId": 123}). Unknown fields are never inspected, so their
/// type never matters.
/// </summary>
internal static class RawOrderReader
{
    public static bool TryRead(string? json, out RawOrder? raw)
    {
        raw = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return false;
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!TryGetOptionalString(root, "orderId", out var orderId)) return false;
            if (!TryGetOptionalString(root, "patientId", out var patientId)) return false;
            if (!TryGetOptionalString(root, "specimenId", out var specimenId)) return false;
            if (!TryGetOptionalString(root, "specimenType", out var specimenType)) return false;
            if (!TryGetOptionalString(root, "priority", out var priority)) return false;
            if (!TryGetOptionalString(root, "collectionDate", out var collectionDate)) return false;
            if (!TryGetOptionalStringArray(root, "requestedTests", out var requestedTests)) return false;

            raw = new RawOrder(orderId, patientId, specimenId, specimenType, priority, collectionDate, requestedTests);
            return true;
        }
    }

    /// <summary>Missing or JSON null => value stays null (ok). Present but not a string => incompatible type.</summary>
    private static bool TryGetOptionalString(JsonElement obj, string name, out string? value)
    {
        value = null;
        if (!obj.TryGetProperty(name, out var prop))
        {
            return true;
        }
        if (prop.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        if (prop.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = prop.GetString();
        return true;
    }

    /// <summary>Missing or JSON null => value stays null (ok). Present but not an array of strings => incompatible type.</summary>
    private static bool TryGetOptionalStringArray(JsonElement obj, string name, out List<string>? values)
    {
        values = null;
        if (!obj.TryGetProperty(name, out var prop))
        {
            return true;
        }
        if (prop.ValueKind == JsonValueKind.Null)
        {
            return true;
        }
        if (prop.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var list = new List<string>();
        foreach (var item in prop.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            list.Add(item.GetString() ?? string.Empty);
        }

        values = list;
        return true;
    }
}

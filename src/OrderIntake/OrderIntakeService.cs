using System.Globalization;

namespace OrderIntake;

/// <summary>
/// Validates a laboratory order submitted as a JSON string and returns an
/// <see cref="OrderResult"/>. Never throws for bad input — every failure
/// mode is reported through the result instead.
/// </summary>
public sealed class OrderIntakeService
{
    private const int MaxIdLength = 20;

    private static readonly Dictionary<string, SpecimenType> SpecimenTypeLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Blood"] = OrderIntake.SpecimenType.Blood,
            ["Urine"] = OrderIntake.SpecimenType.Urine,
            ["Tissue"] = OrderIntake.SpecimenType.Tissue,
            ["Saliva"] = OrderIntake.SpecimenType.Saliva,
        };

    private static readonly Dictionary<string, Priority> PriorityLookup =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Routine"] = OrderIntake.Priority.Routine,
            ["Urgent"] = OrderIntake.Priority.Urgent,
        };

    public OrderResult Process(string json)
    {
        if (!RawOrderReader.TryRead(json, out var raw) || raw is null)
        {
            return OrderResult.Rejected(new[]
            {
                new ValidationError("$", "MALFORMED_INPUT", "Input is not a JSON object shaped like a laboratory order.")
            });
        }

        var errors = new List<ValidationError>();

        ValidateId(raw.OrderId, "orderId", errors);
        ValidateId(raw.PatientId, "patientId", errors);
        ValidateId(raw.SpecimenId, "specimenId", errors);

        var specimenType = ValidateLookup(raw.SpecimenType, "specimenType", SpecimenTypeLookup, errors);
        var priority = ValidateLookup(raw.Priority, "priority", PriorityLookup, errors);
        var collectionDate = ValidateCollectionDate(raw.CollectionDate, errors);
        var requestedTests = ValidateRequestedTests(raw.RequestedTests, errors);

        if (errors.Count > 0)
        {
            return OrderResult.Rejected(errors);
        }

        var order = new LabOrder(
            raw.OrderId!,
            raw.PatientId!,
            raw.SpecimenId!,
            specimenType!.Value,
            priority!.Value,
            collectionDate!.Value,
            requestedTests!);

        return OrderResult.Accepted(order);
    }

    private static void ValidateId(string? value, string field, List<ValidationError> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(field, "REQUIRED", $"{field} is required."));
            return;
        }

        if (value.Length > MaxIdLength)
        {
            errors.Add(new ValidationError(field, "MAX_LENGTH", $"{field} must be at most {MaxIdLength} characters."));
        }
    }

    private static TEnum? ValidateLookup<TEnum>(
        string? value,
        string field,
        Dictionary<string, TEnum> lookup,
        List<ValidationError> errors) where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(field, "REQUIRED", $"{field} is required."));
            return null;
        }

        if (lookup.TryGetValue(value, out var match))
        {
            return match;
        }

        errors.Add(new ValidationError(field, "INVALID_VALUE", $"'{value}' is not a recognized {field}."));
        return null;
    }

    private static DateOnly? ValidateCollectionDate(string? value, List<ValidationError> errors)
    {
        const string field = "collectionDate";

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(field, "REQUIRED", $"{field} is required."));
            return null;
        }

        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors.Add(new ValidationError(field, "INVALID_FORMAT", $"'{value}' is not a valid yyyy-MM-dd date."));
            return null;
        }

        if (date > DateOnly.FromDateTime(DateTime.Today))
        {
            errors.Add(new ValidationError(field, "FUTURE_DATE", $"{field} cannot be after today."));
            return null;
        }

        return date;
    }

    private static List<string>? ValidateRequestedTests(List<string>? value, List<ValidationError> errors)
    {
        const string field = "requestedTests";

        if (value is null || value.Count == 0)
        {
            errors.Add(new ValidationError(field, "REQUIRED", $"{field} must contain at least one test."));
            return null;
        }

        var hasEmptyItem = value.Any(string.IsNullOrWhiteSpace);
        if (hasEmptyItem)
        {
            errors.Add(new ValidationError(field, "INVALID_VALUE", $"{field} must not contain empty test names."));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hasDuplicate = value.Any(test => !seen.Add(test));
        if (hasDuplicate)
        {
            errors.Add(new ValidationError(field, "DUPLICATE", $"{field} contains duplicate test names (case-insensitive)."));
        }

        return hasEmptyItem || hasDuplicate ? null : value;
    }
}

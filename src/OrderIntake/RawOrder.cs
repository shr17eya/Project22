namespace OrderIntake;

/// <summary>
/// The order's fields after JSON parsing, before business-rule validation.
/// Each string field is null when the field was missing or explicitly null
/// in the JSON. A non-null RawOrder means the JSON was a well-formed object
/// whose recognized fields had compatible JSON types (string / string array).
/// </summary>
internal sealed record RawOrder(
    string? OrderId,
    string? PatientId,
    string? SpecimenId,
    string? SpecimenType,
    string? Priority,
    string? CollectionDate,
    List<string>? RequestedTests);

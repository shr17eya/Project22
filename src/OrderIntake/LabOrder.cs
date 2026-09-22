namespace OrderIntake;

/// <summary>
/// A fully validated laboratory order. Only ever produced when every
/// validation rule passes.
/// </summary>
public sealed class LabOrder
{
    public string OrderId { get; }
    public string PatientId { get; }
    public string SpecimenId { get; }
    public SpecimenType SpecimenType { get; }
    public Priority Priority { get; }
    public DateOnly CollectionDate { get; }

    /// <summary>Requested test names, kept in the sender's original casing.</summary>
    public IReadOnlyList<string> RequestedTests { get; }

    public LabOrder(
        string orderId,
        string patientId,
        string specimenId,
        SpecimenType specimenType,
        Priority priority,
        DateOnly collectionDate,
        IReadOnlyList<string> requestedTests)
    {
        OrderId = orderId;
        PatientId = patientId;
        SpecimenId = specimenId;
        SpecimenType = specimenType;
        Priority = priority;
        CollectionDate = collectionDate;
        RequestedTests = requestedTests;
    }
}

namespace OrderIntake;

/// <summary>
/// The outcome of processing one order: either Accepted (with the parsed
/// order and no errors) or Rejected (with no order and every error found).
/// </summary>
public sealed class OrderResult
{
    public OrderStatus Status { get; }
    public LabOrder? Order { get; }
    public IReadOnlyList<ValidationError> Errors { get; }

    private OrderResult(OrderStatus status, LabOrder? order, IReadOnlyList<ValidationError> errors)
    {
        Status = status;
        Order = order;
        Errors = errors;
    }

    public static OrderResult Accepted(LabOrder order) =>
        new(OrderStatus.Accepted, order, Array.Empty<ValidationError>());

    public static OrderResult Rejected(IReadOnlyList<ValidationError> errors) =>
        new(OrderStatus.Rejected, null, errors);
}

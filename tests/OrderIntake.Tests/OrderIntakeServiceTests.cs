using System.Text.Json;
using OrderIntake;

namespace OrderIntake.Tests;

public class OrderIntakeServiceTests
{
    private readonly OrderIntakeService _service = new();

    // ---- shared helpers -------------------------------------------------

    private static string Today() => DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd");
    private static string Tomorrow() => DateOnly.FromDateTime(DateTime.Today).AddDays(1).ToString("yyyy-MM-dd");

    /// <summary>A baseline set of fields that, unmodified, always passes validation.</summary>
    private static Dictionary<string, object?> ValidFields() => new()
    {
        ["orderId"] = "ORD-1005",
        ["patientId"] = "PAT-505",
        ["specimenId"] = "SP-9005",
        ["specimenType"] = "blood",
        ["priority"] = "urgent",
        ["collectionDate"] = Today(),
        ["requestedTests"] = new[] { "Glucose", "CompleteBloodCount" },
    };

    private static string Json(Dictionary<string, object?> fields) => JsonSerializer.Serialize(fields);

    // ---- Group 1: Accepted order -----------------------------------------

    [Fact]
    public void AcceptedOrder_NormalizesCasingAndIgnoresUnknownFields()
    {
        var fields = ValidFields();
        fields["specimenType"] = "BLOOD";
        fields["priority"] = "Urgent";
        fields["senderNote"] = "ignore me";

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.Empty(result.Errors);
        Assert.NotNull(result.Order);
        Assert.Equal("ORD-1005", result.Order!.OrderId);
        Assert.Equal("PAT-505", result.Order.PatientId);
        Assert.Equal("SP-9005", result.Order.SpecimenId);
        Assert.Equal(SpecimenType.Blood, result.Order.SpecimenType);
        Assert.Equal(Priority.Urgent, result.Order.Priority);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Today), result.Order.CollectionDate);
        Assert.Equal(new[] { "Glucose", "CompleteBloodCount" }, result.Order.RequestedTests);
    }

    // ---- Group 2: All errors at once --------------------------------------

    [Fact]
    public void RejectedOrder_ReportsEveryFieldError_NotJustTheFirst()
    {
        var fields = ValidFields();
        fields["orderId"] = "";                 // -> REQUIRED
        fields["specimenType"] = "Plasma";       // -> INVALID_VALUE
        fields["priority"] = "High";             // -> INVALID_VALUE
        fields["collectionDate"] = "2026/01/01"; // -> INVALID_FORMAT
        fields["requestedTests"] = Array.Empty<string>(); // -> REQUIRED

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);

        var actual = result.Errors.Select(e => (e.Field, e.Code)).ToHashSet();
        var expected = new HashSet<(string, string)>
        {
            ("orderId", "REQUIRED"),
            ("specimenType", "INVALID_VALUE"),
            ("priority", "INVALID_VALUE"),
            ("collectionDate", "INVALID_FORMAT"),
            ("requestedTests", "REQUIRED"),
        };

        Assert.Equal(expected, actual);
    }

    // ---- Group 3: ID length -------------------------------------------------

    [Fact]
    public void OrderId_ExactlyTwentyCharacters_IsAccepted()
    {
        var fields = ValidFields();
        fields["orderId"] = new string('A', 20);

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Accepted, result.Status);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void OrderId_TwentyOneCharacters_ProducesMaxLength()
    {
        var fields = ValidFields();
        fields["orderId"] = new string('A', 21);

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "orderId" && e.Code == "MAX_LENGTH");
    }

    // ---- Group 4: Collection date -------------------------------------------

    [Theory]
    [InlineData("2026-02-30")] // not a real calendar date
    [InlineData("2026-9-2")]   // not zero-padded / wrong format
    public void CollectionDate_InvalidFormatOrImpossibleDate_IsRejected(string badDate)
    {
        var fields = ValidFields();
        fields["collectionDate"] = badDate;

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "collectionDate" && e.Code == "INVALID_FORMAT");
    }

    [Fact]
    public void CollectionDate_InTheFuture_IsRejected()
    {
        var fields = ValidFields();
        fields["collectionDate"] = Tomorrow(); // computed from today, not hardcoded

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "collectionDate" && e.Code == "FUTURE_DATE");
    }

    // ---- Group 5: Requested tests --------------------------------------------

    [Fact]
    public void RequestedTests_EmptyList_IsRejected()
    {
        var fields = ValidFields();
        fields["requestedTests"] = Array.Empty<string>();

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "requestedTests" && e.Code == "REQUIRED");
    }

    [Fact]
    public void RequestedTests_NamesDifferingOnlyByCase_AreRejectedAsDuplicates()
    {
        var fields = ValidFields();
        fields["requestedTests"] = new[] { "Glucose", "glucose" };

        var result = _service.Process(Json(fields));

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "requestedTests" && e.Code == "DUPLICATE");
    }

    // ---- Group 6: Broken JSON -------------------------------------------------

    [Theory]
    [InlineData("{ this is not valid json")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("{\"orderId\": 123}")]
    public void MalformedInput_IsRejectedWithSingleMalformedInputError_AndDoesNotThrow(string? badJson)
    {
        var result = _service.Process(badJson!);

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);
        Assert.Single(result.Errors);
        Assert.Equal("$", result.Errors[0].Field);
        Assert.Equal("MALFORMED_INPUT", result.Errors[0].Code);
    }

    [Fact]
    public void EmptyJsonObject_GetsFieldErrors_NotMalformedInput()
    {
        var result = _service.Process("{}");

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Null(result.Order);

        var actual = result.Errors.Select(e => (e.Field, e.Code)).ToHashSet();
        var expected = new HashSet<(string, string)>
        {
            ("orderId", "REQUIRED"),
            ("patientId", "REQUIRED"),
            ("specimenId", "REQUIRED"),
            ("specimenType", "REQUIRED"),
            ("priority", "REQUIRED"),
            ("collectionDate", "REQUIRED"),
            ("requestedTests", "REQUIRED"),
        };

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ExplicitNullField_IsTreatedSameAsMissingField()
    {
        var result = _service.Process("{\"orderId\": null}");

        Assert.Equal(OrderStatus.Rejected, result.Status);
        Assert.Contains(result.Errors, e => e.Field == "orderId" && e.Code == "REQUIRED");
    }
}

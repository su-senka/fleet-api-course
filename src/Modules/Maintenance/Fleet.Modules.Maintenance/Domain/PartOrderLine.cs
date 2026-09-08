using Fleet.Common.Results;

namespace Fleet.Modules.Maintenance.Domain;

/// <summary>
/// Parts of one kind on one work order.
/// </summary>
/// <remarks>
/// The primary key is (<see cref="WorkOrderId"/>, <see cref="PartNumber"/>) rather than a
/// surrogate id. That is not a saving of one column: it is the rule "a part appears at most once
/// per work order", written where the database can enforce it.
/// </remarks>
internal sealed class PartOrderLine
{
    private PartOrderLine() => PartNumber = string.Empty;

    private PartOrderLine(Guid workOrderId, string partNumber, int quantity, decimal unitPrice)
    {
        WorkOrderId = workOrderId;
        PartNumber = partNumber;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public const int PartNumberMaxLength = 40;

    public Guid WorkOrderId { get; private set; }

    public string PartNumber { get; private set; }

    public int Quantity { get; private set; }

    /// <summary>Price per unit, in czech crowns. Stored as numeric, never as a float.</summary>
    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => Quantity * UnitPrice;

    public static Result<PartOrderLine> Create(
        Guid workOrderId,
        string partNumber,
        int quantity,
        decimal unitPrice)
    {
        var normalised = partNumber?.Trim().ToUpperInvariant() ?? string.Empty;

        if (normalised.Length == 0)
        {
            return Error.Validation("part_line.number_required", "A part line needs a part number.");
        }

        if (normalised.Length > PartNumberMaxLength)
        {
            return Error.Validation(
                "part_line.number_too_long",
                $"A part number is at most {PartNumberMaxLength} characters.");
        }

        if (quantity <= 0)
        {
            return Error.Validation("part_line.quantity_not_positive", "Order at least one of something.");
        }

        if (unitPrice < 0)
        {
            return Error.Validation("part_line.price_negative", "A part cannot cost less than nothing.");
        }

        return new PartOrderLine(workOrderId, normalised, quantity, unitPrice);
    }

    /// <summary>Adds to the quantity, and takes the newer price.</summary>
    internal void IncreaseBy(int quantity, decimal unitPrice)
    {
        Quantity += quantity;
        UnitPrice = unitPrice;
    }

    internal static string Normalise(string? partNumber) =>
        partNumber?.Trim().ToUpperInvariant() ?? string.Empty;
}

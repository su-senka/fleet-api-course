namespace Fleet.Modules.Maintenance.Contracts;

/// <summary>Why the vehicle is in the workshop.</summary>
public enum WorkOrderKind
{
    /// <summary>Routine servicing, scheduled by mileage or date.</summary>
    Service = 1,

    /// <summary>Something broke.</summary>
    Repair = 2,

    /// <summary>A statutory or internal inspection.</summary>
    Inspection = 3,

    /// <summary>Tyres. Frequent enough in a fleet to be worth its own category.</summary>
    Tyres = 4,
}

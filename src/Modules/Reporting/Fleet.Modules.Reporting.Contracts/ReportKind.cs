namespace Fleet.Modules.Reporting.Contracts;

/// <summary>The reports this system knows how to produce.</summary>
public enum ReportKind
{
    /// <summary>
    /// Hours booked against hours available, per vehicle, per month, as a CSV.
    /// </summary>
    MonthlyFleetUtilisation = 1,
}

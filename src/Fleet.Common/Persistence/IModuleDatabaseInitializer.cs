namespace Fleet.Common.Persistence;

/// <summary>
/// Brings one module's schema up to date and, on request, fills it with seed data.
/// </summary>
/// <remarks>
/// Each module registers exactly one of these. The host resolves all of them and runs them in
/// <see cref="Order"/>, which is how <c>make reset</c> ends up with a populated database without
/// the host knowing which modules exist.
/// </remarks>
public interface IModuleDatabaseInitializer
{
    /// <summary>The module's name, used for log output. For example <c>vehicles</c>.</summary>
    string ModuleName { get; }

    /// <summary>
    /// Ordering hint, ascending. Modules whose seed data refers to another module's ids run
    /// later - Bookings after Vehicles and Drivers - even though there are no cross-schema
    /// foreign keys to enforce it.
    /// </summary>
    int Order { get; }

    /// <summary>Applies any pending EF Core migrations for this module's schema.</summary>
    Task MigrateAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Inserts the seed data. Must be safe to call twice: an already-seeded schema is left alone
    /// rather than duplicated.
    /// </summary>
    Task SeedAsync(CancellationToken cancellationToken = default);
}

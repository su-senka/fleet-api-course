using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Fleet.Modules.Bookings.Persistence;

/// <summary>
/// Lets <c>dotnet ef</c> build a context without starting a host:
/// <code>
/// dotnet ef migrations add &lt;Name&gt; --project src/Modules/Bookings/Fleet.Modules.Bookings
/// </code>
/// </summary>
internal sealed class BookingsDbContextFactory : IDesignTimeDbContextFactory<BookingsDbContext>
{
    private const string DesignTimeConnectionString =
        "Host=localhost;Port=5432;Database=fleet;Username=fleet;Password=fleet";

    public BookingsDbContext CreateDbContext(string[] args)
    {
        var connectionString = args.Length > 0 ? args[0] : DesignTimeConnectionString;

        var options = new DbContextOptionsBuilder<BookingsDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable(
                BookingsDbContext.MigrationsHistoryTable,
                BookingsDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .Options;

        return new BookingsDbContext(options);
    }
}

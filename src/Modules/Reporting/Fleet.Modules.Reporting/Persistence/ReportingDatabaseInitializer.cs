using Fleet.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Reporting.Persistence;

/// <summary>
/// Migrates the <c>reporting</c> schema. Nothing to seed - a report job exists because somebody
/// asked for one.
/// </summary>
internal sealed class ReportingDatabaseInitializer(ReportingDbContext dbContext) : IModuleDatabaseInitializer
{
    public string ModuleName => ReportingDbContext.Schema;

    public int Order => 60;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

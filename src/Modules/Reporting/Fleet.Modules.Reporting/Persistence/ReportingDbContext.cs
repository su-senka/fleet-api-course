using Fleet.Common.Persistence;
using Fleet.Modules.Reporting.Domain;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Reporting.Persistence;

/// <summary>The Reporting module's own <c>DbContext</c>, in the <c>reporting</c> schema.</summary>
internal sealed class ReportingDbContext(DbContextOptions<ReportingDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public const string Schema = "reporting";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<ReportJob> ReportJobs => Set<ReportJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReportingDbContext).Assembly);
    }
}

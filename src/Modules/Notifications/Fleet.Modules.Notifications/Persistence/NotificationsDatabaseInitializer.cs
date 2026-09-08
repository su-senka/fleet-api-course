using Fleet.Common.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Fleet.Modules.Notifications.Persistence;

/// <summary>
/// Migrates the <c>notifications</c> schema. There is nothing to seed.
/// </summary>
/// <remarks>
/// Deliberately empty. Notifications appear because Drivers announced something, and seeding rows
/// by hand would hide the one interesting thing about this module - that its data arrives through
/// an event and no other way. Start the API, wait for the first outbox sweep, and watch them turn up.
/// </remarks>
internal sealed class NotificationsDatabaseInitializer(NotificationsDbContext dbContext) : IModuleDatabaseInitializer
{
    public string ModuleName => NotificationsDbContext.Schema;

    public int Order => 50;

    public Task MigrateAsync(CancellationToken cancellationToken = default) =>
        dbContext.Database.MigrateAsync(cancellationToken);

    public Task SeedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}

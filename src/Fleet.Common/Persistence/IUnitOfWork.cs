namespace Fleet.Common.Persistence;

/// <summary>
/// Commits the changes tracked by one module's <c>DbContext</c>.
/// </summary>
/// <remarks>
/// <para>
/// Every module implements this over its own <c>DbContext</c>. There is deliberately no
/// repository abstraction underneath: application services query <c>DbContext</c> with LINQ
/// directly. A generic repository over EF Core would hide the very things this course is about -
/// change tracking, query translation and the N+1 problem.
/// </para>
/// <para>
/// The unit of work is per module, so a single HTTP request that touches two modules commits
/// twice. That is the price of schema-per-module, and the reason cross-module state changes
/// travel through the outbox rather than a shared transaction.
/// </para>
/// </remarks>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

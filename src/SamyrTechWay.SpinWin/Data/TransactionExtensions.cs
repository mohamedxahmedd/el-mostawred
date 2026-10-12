using Microsoft.EntityFrameworkCore;

namespace SamyrTechWay.SpinWin.Data;

public static class TransactionExtensions
{
    /// <summary>
    /// Runs <paramref name="work"/> in a transaction inside the configured execution strategy (SQL Server retry on
    /// transient faults). The change tracker is cleared before each attempt, so the delegate must load what it needs.
    /// The delegate commits by returning; any exception rolls back.
    /// </summary>
    public static Task<T> InTransactionAsync<T>(this AppDbContext db, Func<CancellationToken, Task<T>> work, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async token =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(token);
            var result = await work(token);
            await tx.CommitAsync(token);
            return result;
        }, ct);
    }
}

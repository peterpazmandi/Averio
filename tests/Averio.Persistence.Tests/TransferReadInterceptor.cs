using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Armed only by the concurrent transfer-read regression; other commands pass through.
internal sealed class TransferReadInterceptor : DbCommandInterceptor
{
    public Func<Task>? AfterTransferRead { get; set; }
    public Func<Task>? BeforeAccountLock { get; set; }
    public Func<Task>? AfterAccountLock { get; set; }
    public Guid? HouseholdToPause { get; set; }
    public Func<Task>? AfterHouseholdLock { get; set; }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (BeforeAccountLock is { } callback && IsAccountLock(command))
        {
            BeforeAccountLock = null;
            await callback();
        }
        return result;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (AfterHouseholdLock is { } householdLocked
            && command.CommandText.Contains("FOR UPDATE")
            && command.CommandText.Contains("FROM households", StringComparison.OrdinalIgnoreCase)
            && command.Parameters.Cast<DbParameter>().Any(p => p.Value is Guid id && id == HouseholdToPause))
        {
            AfterHouseholdLock = null;
            await householdLocked();
        }
        if (AfterAccountLock is { } locked && IsAccountLock(command))
        {
            AfterAccountLock = null;
            await locked();
        }
        if (AfterTransferRead is { } callback
            && command.CommandText.Contains("FROM transfers", StringComparison.OrdinalIgnoreCase))
        {
            AfterTransferRead = null;
            await callback();
        }
        return result;
    }

    private static bool IsAccountLock(DbCommand command) => command.CommandText.Contains("FOR UPDATE")
        && command.CommandText.Contains("FROM accounts", StringComparison.OrdinalIgnoreCase);
}

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Armed only by the concurrent transfer-read regression; other commands pass through.
internal sealed class TransferReadInterceptor : DbCommandInterceptor
{
    public Func<Task>? AfterTransferRead { get; set; }
    public Func<Task>? BeforeAccountLock { get; set; }
    public Func<Task>? AfterAccountLock { get; set; }

    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        if (BeforeAccountLock is { } callback && command.CommandText.Contains("FOR UPDATE"))
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
        if (AfterAccountLock is { } locked && command.CommandText.Contains("FOR UPDATE"))
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
}

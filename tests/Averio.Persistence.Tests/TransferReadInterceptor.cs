using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

// Armed only by the concurrent transfer-read regression; other commands pass through.
internal sealed class TransferReadInterceptor : DbCommandInterceptor
{
    public Func<Task>? AfterTransferRead { get; set; }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
        CancellationToken cancellationToken = default)
    {
        if (AfterTransferRead is { } callback
            && command.CommandText.Contains("FROM transfers", StringComparison.OrdinalIgnoreCase))
        {
            AfterTransferRead = null;
            await callback();
        }
        return result;
    }
}

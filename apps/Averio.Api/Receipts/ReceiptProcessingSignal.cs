using System.Threading.Channels;
using Averio.Application.Receipts;

namespace Averio.Api.Receipts;

internal sealed class ReceiptProcessingSignal : IReceiptProcessingSignal
{
    // One wake-up is enough. PostgreSQL holds the actual jobs.
    private readonly Channel<bool> wakeUps = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        SingleReader = true,
        FullMode = BoundedChannelFullMode.DropWrite
    });

    public void Notify() => wakeUps.Writer.TryWrite(true);

    public async Task WaitAsync(CancellationToken ct) => await wakeUps.Reader.ReadAsync(ct);
}

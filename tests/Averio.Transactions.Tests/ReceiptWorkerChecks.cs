using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Infrastructure.Receipts;
using Averio.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

internal static class ReceiptWorkerChecks
{
    public static async Task RunAsync()
    {
        foreach (var failCleanupRead in new[] { false, true })
        {
            var repository = new Repository(failCleanupRead);
            var documents = new Documents();
            var options = new ReceiptOptions { Enabled = true };
            using var services = new ServiceCollection()
                .AddScoped(_ => new ReceiptProcessor(repository, documents, new Extractor(), options, TimeProvider.System))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            using var worker = new ReceiptWorker(services.GetRequiredService<IServiceScopeFactory>(),
                options, Options.Create(new ReceiptStorageOptions { Bucket = "test" }), NullLogger<ReceiptWorker>.Instance);
            await worker.StartAsync(default);
            try
            {
                await repository.CleanupStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (!failCleanupRead) await documents.DeleteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
                // This finishes while deletion is still blocked, or cleanup reads fail.
                await repository.Processed.Task.WaitAsync(TimeSpan.FromSeconds(5));
                if (documents.DeleteCompleted) throw new Exception("Test must keep deletion blocked during extraction.");
            }
            finally
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await worker.StopAsync(timeout.Token);
            }
        }
    }
    private sealed class Documents : IReceiptDocumentStore
    {
        public TaskCompletionSource DeleteStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool DeleteCompleted;
        public Task PutAsync(string key, byte[] bytes, CancellationToken ct) => throw new NotSupportedException();
        public Task<byte[]> GetAsync(string key, CancellationToken ct) => Task.FromResult(new byte[] { 1 });
        public async Task DeleteAsync(string key, CancellationToken ct)
        {
            DeleteStarted.TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            DeleteCompleted = true;
        }
    }
    private sealed class Extractor : IReceiptExtractor
    {
        public Task<ReceiptDraft> ExtractAsync(byte[] bytes, CancellationToken ct)
            => Task.FromResult(new ReceiptDraft("Shop", "2026-10-26", "EUR", "1", [new("Food", "1", "Item")], []));
    }
    private sealed class Repository(bool failCleanupRead) : IReceiptRepository
    {
        public TaskCompletionSource CleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Processed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int claimed;
        private readonly ReceiptScan scan = ReceiptScan.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 64), 1, DateTimeOffset.UtcNow.AddMinutes(-10));
        public Task<IReadOnlyList<ReceiptScan>> CleanupCandidatesAsync(DateTimeOffset now, CancellationToken ct)
        {
            CleanupStarted.TrySetResult();
            if (failCleanupRead) throw new IOException("Injected cleanup database failure.");
            return Task.FromResult<IReadOnlyList<ReceiptScan>>([scan]);
        }
        public Task<bool> ExpireAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.FromResult(true);
        public Task DeferCleanupAsync(Guid id, DateTimeOffset retryAt, CancellationToken ct) => Task.CompletedTask;
        public Task MarkDocumentDeletedAsync(Guid id, DateTimeOffset now, CancellationToken ct) => Task.CompletedTask;
        public Task<ReceiptScan?> ClaimAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (Interlocked.Increment(ref claimed) != 1) return Task.FromResult<ReceiptScan?>(null);
            typeof(ReceiptScan).GetProperty(nameof(ReceiptScan.LeaseToken))!.SetValue(scan, Guid.NewGuid());
            return Task.FromResult<ReceiptScan?>(scan);
        }
        public Task<bool> FinishAsync(Guid id, Guid token, DateTimeOffset now, string? draft, string? failure, bool transient, CancellationToken ct)
        {
            if (draft is null || failure is not null) throw new Exception("Extraction did not complete successfully.");
            Processed.TrySetResult();
            return Task.FromResult(true);
        }
        public Task<bool> PostAsync(Guid id, Guid version, Guid transactionId, string confirmationFingerprint, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public void Add(ReceiptScan row) => throw new NotSupportedException();
        public Task<ReceiptScan?> FindOperationAsync(Guid h, Guid m, Guid op, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountUploadsAsync(Guid h, Guid m, DateTimeOffset since, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> QueueAsync(Guid id, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReceiptScan>> ListAsync(Guid h, Guid m, int skip, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReceiptScan?> GetAsync(Guid h, Guid m, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> DiscardAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    }
}

using Averio.Application.Receipts;
using Averio.Domain.Receipts;
using Averio.Api.Receipts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

internal static class ReceiptWorkerChecks
{
    public static async Task RunAsync()
    {
        foreach (var failCleanupRead in new[] { false, true })
        {
            var repository = new Repository(failCleanupRead);
            var documents = new Documents();
            var options = new ReceiptOptions { Enabled = true };
            var processingLog = new Capture<ReceiptProcessor>();
            var workerLog = new Capture<ReceiptProcessingService>();
            using var services = new ServiceCollection()
                .AddSingleton<IReceiptRepository>(repository)
                .AddScoped(_ => new ReceiptProcessor(repository, documents, new Extractor(), options, TimeProvider.System, logger: processingLog))
                .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
            var processorScope = services.GetRequiredService<IServiceScopeFactory>();
            using var worker = new ReceiptProcessingService(processorScope, options, new ReceiptProcessingSignal(), TimeProvider.System, workerLog);
            using var cleanupCancellation = new CancellationTokenSource();
            using var cleanupScope = processorScope.CreateScope();
            var cleanup = cleanupScope.ServiceProvider.GetRequiredService<ReceiptProcessor>()
                .CleanupAsync(cleanupCancellation.Token);
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
                await cleanupCancellation.CancelAsync();
                try
                {
                    await cleanup.WaitAsync(timeout.Token);
                    throw new Exception("Expected the cleanup run to be canceled or fail.");
                }
                catch (OperationCanceledException) when (!failCleanupRead && cleanup.IsCanceled) { }
                catch (IOException) when (failCleanupRead) { }
            }
            if (!processingLog.Messages.Any(x => x.Contains("Receipt claimed:"))
                || !processingLog.Messages.Any(x => x.Contains("Outcome=ready"))
                || !workerLog.Messages.Any(x => x.Contains("Receipt extraction started in the API")))
                throw new Exception("Receipt lifecycle diagnostics missing.");
            if (string.Join("\n", processingLog.Messages).Contains("PRIVATE_RECEIPT"))
                throw new Exception("Receipt contents leaked into logs.");
        }
        using var disabledServices = new ServiceCollection().BuildServiceProvider();
        var disabledLog = new Capture<ReceiptProcessingService>();
        using var disabledWorker = new ReceiptProcessingService(disabledServices.GetRequiredService<IServiceScopeFactory>(),
            new ReceiptOptions(), new ReceiptProcessingSignal(), TimeProvider.System, disabledLog);
        await disabledWorker.StartAsync(default);
        await disabledWorker.ExecuteTask!.WaitAsync(TimeSpan.FromSeconds(5));
        if (!disabledLog.Messages.Any(x => x.Contains("extraction is disabled")))
            throw new Exception("Disabled worker must explain why queued receipts will not run.");

        var failureLog = new Capture<ReceiptProcessor>();
        var processor = new ReceiptProcessor(new Repository(false, true), new Documents(), new FailedExtractor(),
            new ReceiptOptions { Enabled = true }, TimeProvider.System, logger: failureLog);
        await processor.ProcessNextAsync(default);
        var failureText = string.Join("\n", failureLog.Messages);
        if (!failureText.Contains("Stage=provider") || !failureText.Contains("Outcome=retry") || !failureText.Contains("InvalidOperationException") || failureText.Contains("PRIVATE_RECEIPT"))
            throw new Exception("Failure diagnostics must identify stage/retry without leaking exception contents.");

        await IdleAndUploadAsync();
        await RecoverAtDeadlineAsync();
    }

    private static async Task IdleAndUploadAsync()
    {
        var clock = new TestClock();
        var repository = new Repository(false, initiallyReady: false);
        var signal = new ReceiptProcessingSignal();
        using var services = BuildProcessorServices(repository, clock);
        using var recovery = new ReceiptProcessingService(services.GetRequiredService<IServiceScopeFactory>(),
            new ReceiptOptions { Enabled = true }, signal, clock, NullLogger<ReceiptProcessingService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await recovery.StartAsync(timeout.Token);
        try
        {
            await repository.NoPendingWork.Task.WaitAsync(timeout.Token);
            if (repository.ClaimChecks != 1 || clock.TimerStarted.Task.IsCompleted)
                throw new Exception("Idle recovery must wait for an upload signal without a polling timer.");

            repository.AddJob();
            signal.Notify();
            await repository.Processed.Task.WaitAsync(timeout.Token);
            if (clock.TimerStarted.Task.IsCompleted)
                throw new Exception("A completed scan must not start a polling timer.");
        }
        finally { await recovery.StopAsync(timeout.Token); }
    }

    private static async Task RecoverAtDeadlineAsync()
    {
        var clock = new TestClock();
        var retryAt = clock.GetUtcNow().AddMinutes(2);
        var repository = new Repository(false, dueAt: retryAt);
        using var services = BuildProcessorServices(repository, clock);
        using var recovery = new ReceiptProcessingService(services.GetRequiredService<IServiceScopeFactory>(),
            new ReceiptOptions { Enabled = true }, new ReceiptProcessingSignal(), clock, NullLogger<ReceiptProcessingService>.Instance);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await recovery.StartAsync(timeout.Token);
        try
        {
            var timer = await clock.TimerStarted.Task.WaitAsync(timeout.Token);
            if (timer.Delay != TimeSpan.FromMinutes(2) || repository.Processed.Task.IsCompleted)
                throw new Exception("Startup recovery must wait for the stored retry or lease deadline.");

            clock.Now = retryAt;
            timer.Callback(timer.State);
            await repository.Processed.Task.WaitAsync(timeout.Token);
            await repository.NoPendingWork.Task.WaitAsync(timeout.Token);
            if (clock.TimerCount != 1)
                throw new Exception("Recovery must return to idle after processing the outstanding scan.");
        }
        finally { await recovery.StopAsync(timeout.Token); }
    }

    private static ServiceProvider BuildProcessorServices(Repository repository, TimeProvider clock)
        => new ServiceCollection()
            .AddSingleton<IReceiptRepository>(repository)
            .AddScoped(_ => new ReceiptProcessor(repository, new Documents(), new Extractor(), new ReceiptOptions { Enabled = true }, clock))
            .BuildServiceProvider();

    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 10, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public TaskCompletionSource<(TimeSpan Delay, TimerCallback Callback, object? State)> TimerStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int TimerCount { get; private set; }

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            TimerCount++;
            TimerStarted.TrySetResult((dueTime, callback, state));
            return System.CreateTimer(callback, state, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        }
    }
    private sealed class Capture<T> : ILogger<T>
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) throw new Exception("Do not log raw receipt exceptions.");
            Messages.Enqueue(formatter(state, exception));
        }
    }
    private sealed class FailedExtractor : IReceiptExtractor
    {
        public Task<ReceiptDraft> ExtractAsync(byte[] bytes, CancellationToken ct) => throw new InvalidOperationException("PRIVATE_RECEIPT API key response body");
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
            => Task.FromResult(new ReceiptDraft("PRIVATE_RECEIPT", "2026-10-26", "EUR", "1", [new("PRIVATE_RECEIPT", "1", "Item")], []));
    }
    private sealed class Repository(bool failCleanupRead, bool expectFailure = false,
        bool initiallyReady = true, DateTimeOffset? dueAt = null) : IReceiptRepository
    {
        public TaskCompletionSource CleanupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Processed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource NoPendingWork = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int available = initiallyReady ? 1 : 0;
        public int ClaimChecks { get; private set; }
        public void AddJob() => Interlocked.Exchange(ref available, 1);
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
            ClaimChecks++;
            if (dueAt > now || Interlocked.Exchange(ref available, 0) == 0)
                return Task.FromResult<ReceiptScan?>(null);
            typeof(ReceiptScan).GetProperty(nameof(ReceiptScan.LeaseToken))!.SetValue(scan, Guid.NewGuid());
            return Task.FromResult<ReceiptScan?>(scan);
        }
        public Task<DateTimeOffset?> NextProcessingAtAsync(DateTimeOffset now, CancellationToken ct)
        {
            if (Volatile.Read(ref available) == 1) return Task.FromResult<DateTimeOffset?>(dueAt ?? now);
            NoPendingWork.TrySetResult();
            return Task.FromResult<DateTimeOffset?>(null);
        }

        public Task<bool> FinishAsync(Guid id, Guid token, DateTimeOffset now, string? draft, string? failure, bool transient, CancellationToken ct)
        {
            if (expectFailure ? draft is not null || failure != "processing_failed" || !transient : draft is null || failure is not null)
                throw new Exception("Unexpected extraction result.");
            Processed.TrySetResult();
            return Task.FromResult(true);
        }
        public Task<bool> PostAsync(Guid id, Guid version, Guid transactionId, string confirmationFingerprint, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountMonthlyUploadsAsync(Guid h, DateTimeOffset start, DateTimeOffset end, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlySet<string>> DuplicateHashesAsync(Guid h, Guid m, string[] hashes, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReceiptScan?> FindDuplicateAsync(Guid h, Guid m, Guid id, string hash, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> DeletePostedImageAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public void Add(ReceiptScan row) => throw new NotSupportedException();
        public Task<ReceiptScan?> FindOperationAsync(Guid h, Guid m, Guid op, CancellationToken ct) => throw new NotSupportedException();
        public Task<int> CountUploadsAsync(Guid h, Guid m, DateTimeOffset since, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> QueueAsync(Guid id, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
        public Task<IReadOnlyList<ReceiptScan>> ListAsync(Guid h, Guid m, int skip, CancellationToken ct) => throw new NotSupportedException();
        public Task<ReceiptScan?> GetAsync(Guid h, Guid m, Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<bool> DiscardAsync(Guid id, Guid version, DateTimeOffset now, CancellationToken ct) => throw new NotSupportedException();
    }
}


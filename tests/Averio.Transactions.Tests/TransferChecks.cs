using Averio.Domain.Common;
using Averio.Domain.Transfers;
using Averio.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

internal static class TransferChecks
{
    public static void Invariants()
    {
        var account = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        Transfer Create(Guid destination, decimal sent, decimal received, CurrencyCode currency)
            => Transfer.Create(Guid.NewGuid(), account, destination, Guid.NewGuid(), CurrencyCode.Eur,
                currency, sent, received, new DateOnly(2026, 9, 23), null, now);
        Reject<ArgumentException>(() => Create(account, 1m, 1m, CurrencyCode.Eur));
        Reject<ArgumentException>(() => Create(Guid.NewGuid(), 1m, 2m, CurrencyCode.Eur));
        Reject<ArgumentOutOfRangeException>(() => Create(Guid.NewGuid(), 0m, 0m, CurrencyCode.Eur));
        Reject<ArgumentOutOfRangeException>(() => Create(Guid.NewGuid(), 1.00001m, 1.00001m, CurrencyCode.Eur));
        var transfer = Create(Guid.NewGuid(), 10m, 12.3456m, CurrencyCode.Usd);
        var oldVersion = transfer.UpdatedAt;
        transfer.Update(20m, 24m, new DateOnly(2026, 9, 23), "updated", now.AddDays(-1));
        if (transfer.UpdatedAt <= oldVersion || transfer.UpdatedAt.UtcTicks % 10 != 0)
            throw new Exception("Transfer versions must advance at PostgreSQL precision.");
        transfer.Void(now);
        var version = transfer.UpdatedAt;
        transfer.Void(now.AddDays(1));
        if (transfer.UpdatedAt != version) throw new Exception("Repeated void must be idempotent.");
        Reject<InvalidOperationException>(() => transfer.Update(1m, 1m, new DateOnly(2026, 9, 23), null, now));
    }

    public static void PersistenceModel()
    {
        using var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var model = db.GetService<IDesignTimeModel>().Model;
        var entity = model.FindEntityType(typeof(Transfer))!;
        if (!entity.FindProperty(nameof(Transfer.UpdatedAt))!.IsConcurrencyToken)
            throw new Exception("Transfer concurrency token missing.");
        if (entity.GetCheckConstraints().Count() != 4 || entity.GetForeignKeys().Count() != 3)
            throw new Exception("Transfer integrity constraints missing.");
        if (db.Database.HasPendingModelChanges()) throw new Exception("Migration snapshot does not match the model.");
    }

    private static void Reject<T>(Action action) where T : Exception
    { try { action(); } catch (T) { return; } throw new Exception($"Expected {typeof(T).Name}."); }
}

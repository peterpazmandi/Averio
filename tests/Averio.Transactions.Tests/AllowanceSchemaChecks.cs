using System.Text.Json;
using Averio.Api.ErrorHandling;
using Averio.Api.Localization;
using Averio.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

internal static class AllowanceSchemaChecks
{
    public static void Models()
    {
        using var db = new AverioDbContext(new DbContextOptionsBuilder<AverioDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused;Password=unused").Options);
        var assembly = db.GetService<IMigrationsAssembly>();
        var ids = new[]
        {
            "20261020154408_AddRecurringPersonalMoney",
            "20261021125815_RepairRecurringAllowanceCreationColumns",
            "20261021150000_RepairRecurringAllowanceCreationColumns"
        };
        foreach (var id in ids)
        {
            var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
            CheckOwnership(migration.TargetModel);
        }
        CheckOwnership(assembly.ModelSnapshot!.Model);
        CheckOwnership(db.GetService<IDesignTimeModel>().Model);
        if (db.Database.HasPendingModelChanges())
            throw new Exception("Allowance migration snapshot differs from the runtime model.");
    }

    private static void CheckOwnership(IModel model)
    {
        var account = model.FindEntityType("Averio.Domain.Accounts.Account")!;
        var rule = model.FindEntityType("Averio.Domain.PersonalMoney.RecurringAllowanceRule")!;
        foreach (var name in new[] { "CreationAmount", "CreationEndsOn" })
        {
            if (account.FindProperty(name) is not null || rule.FindProperty(name) is null)
                throw new Exception($"Creation metadata belongs only to allowance rules: {name}.");
        }
        var amount = rule.FindProperty("CreationAmount")!;
        if (amount.IsNullable || amount.GetPrecision() != 19 || amount.GetScale() != 4
            || amount.GetColumnName() != "creation_amount"
            || !rule.FindProperty("CreationEndsOn")!.IsNullable
            || rule.FindProperty("CreationEndsOn")!.GetColumnName() != "creation_ends_on")
            throw new Exception("Creation metadata database mapping is inconsistent.");
    }

    public static async Task ErrorsAsync()
    {
        var path = $"/api/v1/households/{Guid.NewGuid()}/personal-money/schedules";
        var missingAmount = Pg("column r.creation_amount does not exist");
        foreach (var (error, endpoint, status) in new (Exception, string, int)[]
        {
            (missingAmount, path, 503),
            (Pg("column r.creation_ends_on does not exist"), path, 503),
            (Pg("column r.creation_amount does not exist"), path + "/" + Guid.NewGuid(), 503),
            (Pg("column r.creation_amount does not exist"), path + "/" + Guid.NewGuid() + "/history", 503),
            (new DbUpdateException("insert failed", Pg("column \"creation_amount\" of relation \"recurring_allowance_rules\" does not exist")), path, 503),
            (Pg("localized diagnostic", table: "recurring_allowance_rules", column: "creation_ends_on"), path, 503),
            (Pg("relation \"recurring_allowance_rules\" does not exist", PostgresErrorCodes.UndefinedTable), path, 503),
            (missingAmount, "/api/v1/accounts", 500),
            (missingAmount, path + "-other", 500),
            (Pg("column r.creation_amount_extra does not exist"), path, 500),
            (Pg("column r.creation_amount does not exist", table: "accounts", column: "creation_amount"), path, 500),
            (Pg("unrelated error", column: "creation_amount"), path, 500),
            (Pg("column r.other does not exist"), path, 500),
            (Pg("relation \"other\" does not exist", PostgresErrorCodes.UndefinedTable), path, 500)
        })
        {
            await using var services = new ServiceCollection().AddLogging().AddProblemDetails().BuildServiceProvider();
            var context = new DefaultHttpContext { RequestServices = services };
            context.Request.Path = endpoint;
            context.Response.Body = new MemoryStream();
            var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance, new TestLocalizer());
            await handler.TryHandleAsync(context, error, CancellationToken.None);
            context.Response.Body.Position = 0;
            using var result = await JsonDocument.ParseAsync(context.Response.Body);
            var expectedCode = status == 503 ? "recurring_allowance_database_update_required" : "unexpected_error";
            if (context.Response.StatusCode != status
                || result.RootElement.GetProperty("code").GetString() != expectedCode)
                throw new Exception($"Wrong schema error response for {endpoint}: {error.Message}.");
        }
    }

    private static PostgresException Pg(string message, string code = PostgresErrorCodes.UndefinedColumn,
        string? table = null, string? column = null)
        => new(message, "ERROR", "ERROR", code, tableName: table, columnName: column);

    private sealed class TestLocalizer : IStringLocalizer<ErrorMessages>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}

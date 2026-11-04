using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Categories.ManageCategories;
using Averio.Application.Transactions.Common;
using Averio.Domain.Accounts;
using Averio.Domain.Categories;
using Averio.Domain.Households;
using Averio.Domain.Imports;
using Averio.Domain.Transactions;
using Averio.Domain.Transfers;

namespace Averio.Application.Imports;

public sealed partial class ImportHandler(ICurrentUser user, IHouseholdRepository households,
    IAccountRepository accounts, ICategoryRepository categories, ICategoryManagementRepository categoryManagement,
    IIconCatalogRepository icons, IImportRepository imports, IXlsxImportReader reader,
    ITransactionRepository transactions, Averio.Application.Transfers.ITransferRepository transfers,
    IUnitOfWork unitOfWork, TimeProvider clock)
{
    public async Task<ImportPreview> PreviewAsync(Guid householdId, Stream file, CancellationToken ct, string? sourceName = null)
    {
        await MemberAsync(householdId, ct);
        var rows = reader.Read(file);
        var canonical = rows.Select(r => JsonSerializer.Serialize(r with { Sheet = "", RowNumber = 0 })).Order(StringComparer.Ordinal);
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', canonical))));
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.FindAsync(householdId, member.Id, fingerprint, ct);
        if (batch is null)
        {
            batch = ImportBatch.Create(householdId, member.Id, fingerprint, JsonSerializer.Serialize(rows), clock.GetUtcNow(), CleanSourceName(sourceName));
            imports.Add(batch);
            await unitOfWork.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
        return Preview(batch);
    }
    public async Task<ImportPreview> GetAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var member = await MemberAsync(householdId, ct);
        return Preview(await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id));
    }
    public async Task<ImportResult> ConfirmAsync(Guid householdId, Guid id, ConfirmImportRequest request, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        // The household lock serializes this operation with other imports, account changes and joining/leaving.
        var member = await MemberAsync(householdId, ct);
        var batch = await imports.GetAsync(householdId, member.Id, id, ct) ?? throw new NotFoundException("import", id);
        var rows = Rows(batch);
        if (batch.CompletedAt is not null) return Result(batch, rows);
        if (!request.AcknowledgeBalancesAndOverlap || request.Accounts is null || request.Categories is null
            || request.Accounts.Count > 100 || request.Categories.Count > 1000
            || request.Accounts.Any(a => a is null) || request.Categories.Any(c => c is null)) throw MappingError();
        var sources = rows.SelectMany(r => r.Destination is null ? new[] { r.Account } : new[] { r.Account, r.Destination }).Distinct().ToArray();
        if (request.Accounts.Count != sources.Length || request.Accounts.Select(a => a.Source).Distinct().Count() != sources.Length
            || request.Accounts.Any(a => !sources.Contains(a.Source))
            || request.Accounts.Select(a => a.AccountId).Distinct().Count() != sources.Length) throw MappingError();
        var accountMap = new Dictionary<string, (Account Account, TransactionVisibility Visibility)>();
        foreach (var mapping in request.Accounts)
        {
            var account = await accounts.GetVisibleByIdAsync(householdId, member.Id, mapping.AccountId, ct)
                ?? throw new NotFoundException("account", mapping.AccountId);
            if (!TransactionAccess.CanModify(account, member)) throw new ForbiddenException();
            if (account.Status != AccountStatus.Active) throw new ConflictException("account_archived", "Error_AccountArchived");
            if (!Enum.TryParse<TransactionVisibility>(mapping.Visibility, out var visibility) || !Enum.IsDefined(visibility)
                || !TransactionAccess.IsVisibilityAllowed(account, visibility)) throw MappingError();
            accountMap.Add(mapping.Source, (account, visibility));
        }
        var categorySources = rows.Where(r => r.Kind != "Transfer").Select(r => (r.Kind, r.Category)).Distinct().ToArray();
        if (request.Categories.Count != categorySources.Length
            || request.Categories.Select(c => (c.Kind, c.Source)).Distinct().Count() != categorySources.Length
            || request.Categories.Any(c => !categorySources.Contains((c.Kind, c.Source)))) throw MappingError();
        var ids = request.Categories.Where(c => c.CategoryId is not null).Select(c => c.CategoryId!.Value).Distinct().ToArray();
        var existing = await categories.GetAvailableByIdsAsync(householdId, ids, ct);
        var icon = (await icons.GetAllAsync(ct)).FirstOrDefault()?.Value ?? throw MappingError();
        var categoryMap = new Dictionary<(string, string), Category>();
        foreach (var mapping in request.Categories)
        {
            var kind = Enum.Parse<TransactionKind>(mapping.Kind);
            Category category;
            if (mapping.CategoryId is Guid categoryId)
            {
                category = existing.SingleOrDefault(c => c.Id == categoryId) ?? throw MappingError();
                if (category.IsArchived || !category.Supports(kind)) throw MappingError();
            }
            else
            {
                category = Category.Create(householdId, mapping.Source,
                    kind == TransactionKind.Income ? CategoryKind.Income : CategoryKind.Expense,
                    icon, "#10B981", null, clock.GetUtcNow());
                categoryManagement.Add(category);
            }
            categoryMap.Add((mapping.Kind, mapping.Source), category);
        }
        var transactionIds = new List<Guid>();
        var links = new List<ImportRecordLink>();
        foreach (var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            var (account, visibility) = accountMap[row.Account];
            if (account.Currency.Value != row.Currency) throw MappingError();
            if (row.Kind == "Transfer")
            {
                var destination = accountMap[row.Destination!].Account;
                var received = row.ReceivedAmount ?? (destination.Currency == account.Currency ? row.Amount : throw MappingError());
                if (row.ReceivedCurrency is not null && destination.Currency.Value != row.ReceivedCurrency
                    || destination.Currency == account.Currency && received != row.Amount) throw MappingError();
                var transfer = Transfer.Create(householdId, account.Id, destination.Id, member.Id,
                    account.Currency, destination.Currency, row.Amount, received, row.Date, row.Note, clock.GetUtcNow());
                transfers.Add(transfer);
                links.Add(new(transfer.Id, row.Kind, StoredVersion(transfer.UpdatedAt), account.Name, destination.Name, null, false));
            }
            else
            {
                var category = categoryMap[(row.Kind, row.Category)];
                var transaction = Transaction.Create(householdId, account.Id, member.Id,
                    Enum.Parse<TransactionKind>(row.Kind), visibility, account.Currency, row.Tags, row.Note, row.Date,
                    [new TransactionItemDraft(row.Category, category.Id, category.NameReference, category.Icon, row.Amount)], [], clock.GetUtcNow());
                transactions.Add(transaction); transactionIds.Add(transaction.Id);
                links.Add(new(transaction.Id, row.Kind, StoredVersion(transaction.UpdatedAt), account.Name, null, category.NameReference, category.IsBase));
            }
        }
        batch.Complete(clock.GetUtcNow(), JsonSerializer.Serialize(links), JsonSerializer.Serialize(Summarize(rows)));
        await unitOfWork.SaveChangesAsync(ct);
        await imports.ApplyAggregatesAsync(transactionIds.ToArray(), ct);
        await tx.CommitAsync(ct);
        return Result(batch, rows);
    }
    private async Task<HouseholdMember> MemberAsync(Guid householdId, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct) ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(m => m.UserId == userId && m.LeftAtUtc is null);
    }
    private static BusinessRuleException MappingError() => new("import_mapping_invalid", "Error_ImportMapping");
    private static ImportRow[] Rows(ImportBatch batch) => JsonSerializer.Deserialize<ImportRow[]>(batch.Payload)!;
    private static ImportResult Result(ImportBatch batch, IReadOnlyList<ImportRow> rows)
        => new(batch.Id, "Completed", rows.Count(r => r.Kind != "Transfer"), rows.Count(r => r.Kind == "Transfer"));
    private static ImportPreview Preview(ImportBatch batch)
    {
        var rows = Rows(batch);
        return new(batch.Id, batch.CompletedAt is null ? "Draft" : "Completed", rows.Length,
            rows.Min(r => r.Date), rows.Max(r => r.Date),
            rows.SelectMany(r => r.Destination is null ? new[] { r.Account } : new[] { r.Account, r.Destination }).Distinct().Order().ToArray(),
            rows.Where(r => r.Kind != "Transfer").Select(r => new ImportCategorySource(r.Kind, r.Category)).Distinct().OrderBy(c => c.Kind).ThenBy(c => c.Name).ToArray(),
            rows.GroupBy(r => (r.Kind, r.Currency)).Select(g => new ImportTotal(g.Key.Kind, g.Key.Currency,
                g.Sum(r => r.Amount).ToString(CultureInfo.InvariantCulture), g.Count())).ToArray(),
            rows.GroupBy(r => r with { Sheet = "", RowNumber = 0 }).Sum(g => g.Count() - 1), rows.Select(r => new ImportPreviewRow(r.Sheet, r.RowNumber, r.Kind, r.Date,
                r.Account, r.Destination, r.Category, r.Amount.ToString(CultureInfo.InvariantCulture), r.Currency,
                r.ReceivedAmount?.ToString(CultureInfo.InvariantCulture), r.ReceivedCurrency, r.Tags, r.Note)).ToArray());
    }
}


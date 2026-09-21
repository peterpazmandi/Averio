using Averio.Application.Common.Abstractions;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(AverioDbContext dbContext)
    : ITransactionRepository
{
    public void Add(Transaction transaction) => dbContext.Transactions.Add(transaction);

    public Task<Transaction?> GetVisibleByIdAsync(
        Guid householdId,
        Guid householdMemberId,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        return VisibleTransactions(householdId, householdMemberId)
            .Include(transaction => transaction.Items)
            .Include(transaction => transaction.Labels)
            .AsSplitQuery()
            .SingleOrDefaultAsync(transaction => transaction.Id == transactionId, cancellationToken);
    }

    public async Task<TransactionReadPage> GetVisiblePageAsync(
        Guid householdId,
        Guid householdMemberId,
        TransactionQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = VisibleTransactions(householdId, householdMemberId)
            .AsNoTracking()
            .Join(
                dbContext.Accounts.AsNoTracking(),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new { Transaction = transaction, Account = account });

        if (query.From is not null)
            filtered = filtered.Where(item => item.Transaction.OccurredOn >= query.From);
        if (query.To is not null)
            filtered = filtered.Where(item => item.Transaction.OccurredOn <= query.To);
        if (query.AccountId is not null)
            filtered = filtered.Where(item => item.Transaction.AccountId == query.AccountId);
        if (query.Kind is not null)
            filtered = filtered.Where(item => item.Transaction.Kind == query.Kind);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            filtered = filtered.Where(item =>
                (item.Transaction.Merchant != null
                    && EF.Functions.ILike(item.Transaction.Merchant, pattern))
                || EF.Functions.ILike(item.Transaction.Description, pattern)
                || (item.Transaction.Note != null
                    && EF.Functions.ILike(item.Transaction.Note, pattern))
                || item.Transaction.Items.Any(detail =>
                    EF.Functions.ILike(detail.Description, pattern)
                    || (detail.CategoryName != null
                        && EF.Functions.ILike(detail.CategoryName, pattern)))
                || item.Transaction.Labels.Any(label => EF.Functions.ILike(label.Name, pattern)));
        }

        if (query.CursorDate is not null && query.CursorCreatedAt is not null)
        {
            filtered = filtered.Where(item =>
                item.Transaction.OccurredOn < query.CursorDate
                || (item.Transaction.OccurredOn == query.CursorDate
                    && item.Transaction.CreatedAt < query.CursorCreatedAt));
        }

        var rows = await filtered
            .OrderByDescending(item => item.Transaction.OccurredOn)
            .ThenByDescending(item => item.Transaction.CreatedAt)
            .Take(query.PageSize + 1)
            .Select(item => new TransactionBaseRow(
                item.Transaction.Id,
                item.Transaction.AccountId,
                item.Account.Name,
                item.Transaction.Currency.Value,
                item.Transaction.Kind,
                item.Transaction.Visibility,
                item.Transaction.Amount,
                item.Transaction.Description,
                item.Transaction.Merchant,
                item.Transaction.Note,
                item.Transaction.OccurredOn,
                item.Transaction.Status,
                item.Transaction.CreatedAt,
                item.Transaction.UpdatedAt,
                item.Account.Scope == AccountScope.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.PageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        return new TransactionReadPage(
            await LoadDetailsAsync(rows, cancellationToken),
            hasMore);
    }

    public async Task<IReadOnlyList<TransactionReadModel>> GetLatestVisibleAsync(
        Guid householdId,
        Guid householdMemberId,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await VisibleTransactions(householdId, householdMemberId)
            .AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Posted)
            .Join(
                dbContext.Accounts.AsNoTracking(),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new { Transaction = transaction, Account = account })
            .OrderByDescending(item => item.Transaction.OccurredOn)
            .ThenByDescending(item => item.Transaction.CreatedAt)
            .Take(Math.Clamp(take, 1, 20))
            .Select(item => new TransactionBaseRow(
                item.Transaction.Id,
                item.Transaction.AccountId,
                item.Account.Name,
                item.Transaction.Currency.Value,
                item.Transaction.Kind,
                item.Transaction.Visibility,
                item.Transaction.Amount,
                item.Transaction.Description,
                item.Transaction.Merchant,
                item.Transaction.Note,
                item.Transaction.OccurredOn,
                item.Transaction.Status,
                item.Transaction.CreatedAt,
                item.Transaction.UpdatedAt,
                item.Account.Scope == AccountScope.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)))
            .ToListAsync(cancellationToken);

        return await LoadDetailsAsync(rows, cancellationToken);
    }

    private async Task<IReadOnlyList<TransactionReadModel>> LoadDetailsAsync(
        IReadOnlyList<TransactionBaseRow> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(row => row.Id).ToArray();

        var items = await dbContext.TransactionItems
            .AsNoTracking()
            .Where(item => ids.Contains(item.TransactionId))
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        var labels = await dbContext.TransactionLabels
            .AsNoTracking()
            .Where(label => ids.Contains(label.TransactionId))
            .OrderBy(label => label.Scope)
            .ThenBy(label => label.Name)
            .ToListAsync(cancellationToken);

        var itemsByTransaction = items
            .GroupBy(item => item.TransactionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TransactionItemReadModel>)group
                    .Select(item => new TransactionItemReadModel(
                        item.Id,
                        item.Position,
                        item.Description,
                        item.CategoryName,
                        item.Amount))
                    .ToArray());

        var labelsByTransaction = labels
            .GroupBy(label => label.TransactionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TransactionLabelReadModel>)group
                    .Select(label => new TransactionLabelReadModel(
                        label.Id,
                        label.Name,
                        label.Scope))
                    .ToArray());

        return rows.Select(row => new TransactionReadModel(
            row.Id,
            row.AccountId,
            row.AccountName,
            row.Currency,
            row.Kind,
            row.Visibility,
            row.Amount,
            row.Description,
            row.Merchant,
            row.Note,
            row.OccurredOn,
            row.Status,
            row.CreatedAt,
            row.UpdatedAt,
            row.CanModify,
            itemsByTransaction.GetValueOrDefault(row.Id, []),
            labelsByTransaction.GetValueOrDefault(row.Id, [])))
            .ToArray();
    }

    private IQueryable<Transaction> VisibleTransactions(
        Guid householdId,
        Guid householdMemberId)
    {
        return dbContext.Transactions
            .Join(
                dbContext.Accounts.Include(account => account.Owners),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new { Transaction = transaction, Account = account })
            .Where(item =>
                item.Transaction.HouseholdId == householdId
                && (
                    item.Account.Visibility == AccountVisibility.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
                && (
                    item.Transaction.Visibility == TransactionVisibility.Household
                    || item.Account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                ))
            .Select(item => item.Transaction);
    }

    private sealed record TransactionBaseRow(
        Guid Id,
        Guid AccountId,
        string AccountName,
        string Currency,
        TransactionKind Kind,
        TransactionVisibility Visibility,
        decimal Amount,
        string Description,
        string? Merchant,
        string? Note,
        DateOnly OccurredOn,
        TransactionStatus Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt,
        bool CanModify);
}

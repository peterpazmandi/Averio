using Averio.Application.Common.Abstractions;
using Averio.Domain.Accounts;
using Averio.Domain.Labels;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(AverioDbContext dbContext)
    : ITransactionRepository
{
    public void Add(Transaction transaction)
        => dbContext.Transactions.Add(transaction);

    public Task<Transaction?> GetVisibleByIdAsync(
        Guid householdId,
        Guid householdMemberId,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        return dbContext.VisibleTransactions(householdId, householdMemberId)
            .Include(transaction => transaction.Items)
            .Include(transaction => transaction.Labels)
            .AsSplitQuery()
            .SingleOrDefaultAsync(
                transaction => transaction.Id == transactionId,
                cancellationToken);
    }

    public async Task<TransactionReadPage> GetVisiblePageAsync(
        Guid householdId,
        Guid householdMemberId,
        TransactionQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = dbContext.VisibleTransactions(householdId, householdMemberId)
            .AsNoTracking()
            .Join(
                dbContext.Accounts.AsNoTracking(),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new
                {
                    Transaction = transaction,
                    Account = account
                });

        if (!query.IncludeArchived)
            filtered = filtered.Where(item => !item.Transaction.IsArchived);

        if (query.ActivityId is Guid activityId)
            filtered = filtered.Where(item => dbContext.Set<Averio.Domain.Activities.ActivityAssignment>().Any(link =>
                link.HouseholdId == householdId && link.HouseholdMemberId == householdMemberId
                && link.TransactionId == item.Transaction.Id && link.ActivityId == activityId));

        foreach (var labelId in query.LabelIds ?? [])
            filtered = filtered.Where(item => item.Transaction.Labels.Any(link => link.LabelId == labelId));

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
                    || EF.Functions.ILike(detail.CategoryName, pattern))
                || item.Transaction.Labels.Any(link =>
                    dbContext.Labels.Any(label =>
                        label.Id == link.LabelId
                        && EF.Functions.ILike(label.Name, pattern))));
        }

        if (query.CursorDate is not null
            && query.CursorCreatedAt is not null
            && query.CursorId is not null)
        {
            filtered = filtered.Where(item =>
                item.Transaction.OccurredOn < query.CursorDate
                || (item.Transaction.OccurredOn == query.CursorDate
                    && (
                        item.Transaction.CreatedAt < query.CursorCreatedAt
                        || (item.Transaction.CreatedAt == query.CursorCreatedAt
                            && item.Transaction.Id.CompareTo(
                                query.CursorId.Value) < 0)
                    )));
        }

        var rows = await filtered
            .OrderByDescending(item => item.Transaction.OccurredOn)
            .ThenByDescending(item => item.Transaction.CreatedAt)
            .ThenByDescending(item => item.Transaction.Id)
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
                    || item.Account.Owners.Any(owner =>
                        owner.HouseholdMemberId == householdMemberId)))
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > query.PageSize;
        if (hasMore)
            rows.RemoveAt(rows.Count - 1);

        return new TransactionReadPage(
            await LoadDetailsAsync(rows, householdId, householdMemberId, cancellationToken),
            hasMore);
    }

    public async Task<IReadOnlyList<TransactionReadModel>> GetLatestVisibleAsync(
        Guid householdId,
        Guid householdMemberId,
        int take,
        CancellationToken cancellationToken)
    {
        var rows = await dbContext.VisibleTransactions(householdId, householdMemberId)
            .AsNoTracking()
            .Where(transaction => transaction.Status == TransactionStatus.Posted && !transaction.IsArchived)
            .Join(
                dbContext.Accounts.AsNoTracking(),
                transaction => new { transaction.HouseholdId, transaction.AccountId },
                account => new { account.HouseholdId, AccountId = account.Id },
                (transaction, account) => new
                {
                    Transaction = transaction,
                    Account = account
                })
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
                    || item.Account.Owners.Any(owner =>
                        owner.HouseholdMemberId == householdMemberId)))
            .ToListAsync(cancellationToken);

        return await LoadDetailsAsync(rows, householdId, householdMemberId, cancellationToken);
    }

    private async Task<IReadOnlyList<TransactionReadModel>> LoadDetailsAsync(
        IReadOnlyList<TransactionBaseRow> rows,
        Guid householdId, Guid householdMemberId,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return [];

        var ids = rows.Select(row => row.Id).ToArray();

        var assignments = await (from link in dbContext.Set<Averio.Domain.Activities.ActivityAssignment>().AsNoTracking()
            join activity in dbContext.Set<Averio.Domain.Activities.Activity>() on link.ActivityId equals activity.Id into matches
            from activity in matches.DefaultIfEmpty()
            where link.HouseholdId == householdId && link.HouseholdMemberId == householdMemberId && ids.Contains(link.TransactionId)
            select new { link.TransactionId, link.ActivityId, Name = activity == null ? null : activity.Name, link.Version })
            .ToDictionaryAsync(x => x.TransactionId, cancellationToken);

        var items = await dbContext.TransactionItems
            .AsNoTracking()
            .Where(item => ids.Contains(item.TransactionId))
            .OrderBy(item => item.Position)
            .ToListAsync(cancellationToken);

        var labels = await (
            from link in dbContext.TransactionLabels.AsNoTracking()
            join label in dbContext.Labels.AsNoTracking()
                on new { link.HouseholdId, link.LabelId }
                equals new { label.HouseholdId, LabelId = label.Id }
            where ids.Contains(link.TransactionId)
            select new
            {
                link.TransactionId,
                label.Id,
                label.Name,
                label.Scope,
            })
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
                        item.CategoryId,
                        item.CategoryName,
                        item.CategoryIcon,
                        item.Amount))
                    .ToArray());

        var labelsByTransaction = labels
            .GroupBy(label => label.TransactionId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<TransactionLabelReadModel>)group
                    .OrderBy(label => label.Scope)
                    .ThenBy(label => label.Name)
                    .Select(label => new TransactionLabelReadModel(
                        label.Id,
                        label.Name,
                        label.Scope))
                    .ToArray());

        return rows
            .Select(row => new TransactionReadModel(
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
                labelsByTransaction.GetValueOrDefault(row.Id, []),
                assignments.GetValueOrDefault(row.Id)?.ActivityId,
                assignments.GetValueOrDefault(row.Id)?.Name,
                assignments.GetValueOrDefault(row.Id)?.Version ?? Guid.Empty))
            .ToArray();
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

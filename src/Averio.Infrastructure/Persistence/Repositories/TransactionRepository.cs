using Averio.Application.Common.Abstractions;
using Averio.Domain.Accounts;
using Averio.Domain.Transactions;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(AverioDbContext dbContext)
    : ITransactionRepository
{
    public void Add(Transaction transaction)
    {
        dbContext.Transactions.Add(transaction);
    }

    public Task<Transaction?> GetVisibleByIdAsync(
        Guid householdId,
        Guid householdMemberId,
        Guid transactionId,
        CancellationToken cancellationToken)
    {
        return VisibleTransactions(householdId, householdMemberId)
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
        var filtered = VisibleReadModels(householdId, householdMemberId);

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
                EF.Functions.ILike(item.Transaction.Description, pattern)
                || (item.Transaction.Note != null
                    && EF.Functions.ILike(item.Transaction.Note, pattern)));
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
            .Select(item => new TransactionReadModel(
                item.Transaction.Id,
                item.Transaction.AccountId,
                item.Account.Name,
                item.Transaction.Currency.Value,
                item.Transaction.Kind,
                item.Transaction.Visibility,
                item.Transaction.Amount,
                item.Transaction.Description,
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

        return new TransactionReadPage(rows, hasMore);
    }

    public async Task<IReadOnlyList<TransactionReadModel>> GetLatestVisibleAsync(
        Guid householdId,
        Guid householdMemberId,
        int take,
        CancellationToken cancellationToken)
    {
        return await VisibleReadModels(householdId, householdMemberId)
            .Where(item => item.Transaction.Status == TransactionStatus.Posted)
            .OrderByDescending(item => item.Transaction.OccurredOn)
            .ThenByDescending(item => item.Transaction.CreatedAt)
            .Take(Math.Clamp(take, 1, 20))
            .Select(item => new TransactionReadModel(
                item.Transaction.Id,
                item.Transaction.AccountId,
                item.Account.Name,
                item.Transaction.Currency.Value,
                item.Transaction.Kind,
                item.Transaction.Visibility,
                item.Transaction.Amount,
                item.Transaction.Description,
                item.Transaction.Note,
                item.Transaction.OccurredOn,
                item.Transaction.Status,
                item.Transaction.CreatedAt,
                item.Transaction.UpdatedAt,
                item.Account.Scope == AccountScope.Household
                    || item.Account.Owners.Any(owner =>
                        owner.HouseholdMemberId == householdMemberId)))
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Transaction> VisibleTransactions(
        Guid householdId,
        Guid householdMemberId)
    {
        return from transaction in dbContext.Transactions
            join account in dbContext.Accounts.Include(account => account.Owners)
                on new { transaction.HouseholdId, transaction.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            where transaction.HouseholdId == householdId
                && (
                    account.Visibility == AccountVisibility.Household
                    || account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
                && (
                    transaction.Visibility == TransactionVisibility.Household
                    || account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
            select transaction;
    }

    private IQueryable<TransactionWithAccount> VisibleReadModels(
        Guid householdId,
        Guid householdMemberId)
    {
        return from transaction in dbContext.Transactions.AsNoTracking()
            join account in dbContext.Accounts
                .Include(account => account.Owners)
                .AsNoTracking()
                on new { transaction.HouseholdId, transaction.AccountId }
                equals new { account.HouseholdId, AccountId = account.Id }
            where transaction.HouseholdId == householdId
                && (
                    account.Visibility == AccountVisibility.Household
                    || account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
                && (
                    transaction.Visibility == TransactionVisibility.Household
                    || account.Owners.Any(owner => owner.HouseholdMemberId == householdMemberId)
                )
            select new TransactionWithAccount(transaction, account);
    }

    private sealed record TransactionWithAccount(
        Transaction Transaction,
        Account Account);
}

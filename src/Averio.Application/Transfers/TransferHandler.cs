using Averio.Application.Accounts.Common;
using Averio.Application.PersonalMoney;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Accounts;
using Averio.Domain.Households;
using Averio.Domain.Transfers;
using FluentValidation;

namespace Averio.Application.Transfers;

public sealed class TransferHandler(ICurrentUser currentUser, IHouseholdRepository households,
    ITransferRepository transfers, IValidator<TransferInput> validator,
    IUnitOfWork unitOfWork, TimeProvider timeProvider,
    PersonalMoneyHandler personalMoney)
{
    public async Task<Guid> CreateAsync(Guid householdId, Guid sourceId, Guid destinationId,
        TransferInput input, CancellationToken ct, string? personalMoneyPurpose = null)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        if (sourceId == destinationId)
            throw new BusinessRuleException("transfer_accounts_invalid", "Error_TransferDistinctAccounts");
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var member = await GetMemberAsync(householdId, ct);
        var locked = await transfers.GetLockedAccountsAsync(householdId, sourceId, destinationId, ct);
        var source = GetWritableAccount(member, locked, sourceId, true);
        var destination = GetWritableAccount(member, locked, destinationId, true);
        ValidateAmounts(source, destination, input);
        var transfer = Transfer.Create(householdId, sourceId, destinationId, member.Id,
            source.Currency, destination.Currency, input.SentAmount, input.ReceivedAmount,
            input.OccurredOn, input.Note, timeProvider.GetUtcNow());
        transfers.Add(transfer);
        if (!string.IsNullOrWhiteSpace(personalMoneyPurpose))
        {
            if (!Enum.TryParse<PersonalTransferPurpose>(personalMoneyPurpose, true, out var purpose)
                || !Enum.IsDefined(purpose))
                throw new BusinessRuleException("personal_money_purpose_invalid", "Error_TransferAmountsMismatch");
            await personalMoney.LinkTransferAsync(transfer, member, source, destination, purpose, ct);
        }
        // Both legs are columns of one row: one SaveChanges is an atomic financial mutation.
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return transfer.Id;
    }

    public async Task UpdateAsync(Guid householdId, Guid id, TransferInput input,
        DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var member = await GetMemberAsync(householdId, ct);
        var transfer = await GetWritableAsync(member, id, ct);
        CheckVersion(transfer, expectedUpdatedAt);
        if (transfer.VoidedAt is not null)
            throw new ConflictException("transfer_voided", "Error_TransferVoided");
        var locked = await transfers.GetLockedAccountsAsync(householdId, transfer.SourceAccountId, transfer.DestinationAccountId, ct);
        var source = GetWritableAccount(member, locked, transfer.SourceAccountId, true);
        var destination = GetWritableAccount(member, locked, transfer.DestinationAccountId, true);
        ValidateAmounts(source, destination, input);
        transfer.Update(input.SentAmount, input.ReceivedAmount, input.OccurredOn,
            input.Note, timeProvider.GetUtcNow());
        await personalMoney.CorrectTransferAsync(transfer, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task VoidAsync(Guid householdId, Guid id, DateTimeOffset expectedUpdatedAt, CancellationToken ct)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(ct);
        var member = await GetMemberAsync(householdId, ct);
        var transfer = await GetWritableAsync(member, id, ct);
        var locked = await transfers.GetLockedAccountsAsync(householdId, transfer.SourceAccountId, transfer.DestinationAccountId, ct);
        GetWritableAccount(member, locked, transfer.SourceAccountId, false);
        GetWritableAccount(member, locked, transfer.DestinationAccountId, false);
        if (transfer.VoidedAt is not null) return; // Authorized retries are idempotent.
        CheckVersion(transfer, expectedUpdatedAt);
        transfer.Void(timeProvider.GetUtcNow());
        await personalMoney.CorrectTransferAsync(transfer, ct);
        await unitOfWork.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<TransferResult> GetAsync(Guid householdId, Guid id, CancellationToken ct)
    {
        var member = await GetMemberAsync(householdId, ct);
        return await transfers.GetVisibleAsync(householdId, member.Id, id, ct)
            ?? throw new NotFoundException("transfer", id);
    }

    public async Task<TransferPage> ListAsync(Guid householdId, TransferQuery query, CancellationToken ct)
    {
        var member = await GetMemberAsync(householdId, ct);
        if (query.From > query.To || query.PageSize is < 1 or > 100
            || ((query.CursorDate is not null || query.CursorCreatedAt is not null || query.CursorId is not null)
                && (query.CursorDate is null || query.CursorCreatedAt is null || query.CursorId is null)))
            throw new BusinessRuleException("transfer_query_invalid", "Error_TransferQueryInvalid");
        return await transfers.GetPageAsync(householdId, member.Id, query, ct);
    }

    private async Task<HouseholdMember> GetMemberAsync(Guid householdId, CancellationToken ct)
    {
        var userId = currentUser.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct)
            ?? throw new NotFoundException("household", householdId);
        if (!household.IsFinancialSetupCompleted) throw new OnboardingRequiredException();
        return household.Members.Single(member => member.UserId == userId);
    }

    private static Account GetWritableAccount(HouseholdMember member, IReadOnlyList<Account> locked,
        Guid id, bool requireActive)
    {
        var account = locked.SingleOrDefault(a => a.Id == id
            && (a.Visibility == AccountVisibility.Household
                || a.Owners.Any(o => o.HouseholdMemberId == member.Id)))
            ?? throw new NotFoundException("account", id);
        if (!AccountAccess.CanModify(account, member)) throw new ForbiddenException();
        if (requireActive && account.Status != AccountStatus.Active)
            throw new ConflictException("account_archived", "Error_AccountArchived");
        return account;
    }

    private async Task<Transfer> GetWritableAsync(HouseholdMember member, Guid id, CancellationToken ct)
        => await transfers.GetForUpdateAsync(member.HouseholdId, member.Id, id, ct)
            ?? throw new NotFoundException("transfer", id);

    private static void ValidateAmounts(Account source, Account destination, TransferInput input)
    {
        if (source.Currency == destination.Currency && input.SentAmount != input.ReceivedAmount)
            throw new BusinessRuleException("transfer_amounts_mismatch", "Error_TransferAmountsMismatch");
    }

    private static void CheckVersion(Transfer transfer, DateTimeOffset expected)
    {
        if (transfer.UpdatedAt != expected)
            throw new ConflictException("transfer_changed", "Error_TransferChanged");
    }
}

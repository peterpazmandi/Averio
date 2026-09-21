using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Transactions.Common;
using Averio.Domain.Labels;

namespace Averio.Application.Labels.GetLabels;

public sealed class GetLabelsHandler(
    ICurrentUser currentUser,
    IHouseholdRepository householdRepository,
    IAccountRepository accountRepository,
    ILabelRepository labelRepository)
{
    public async Task<IReadOnlyList<LabelResult>> HandleAsync(
        Guid householdId,
        Guid? accountId,
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var household = await householdRepository.GetByIdForUserAsync(
            householdId,
            userId,
            cancellationToken)
            ?? throw new NotFoundException("household", householdId);

        var membership = household.Members.Single(member => member.UserId == userId);

        if (accountId is not null)
        {
            var account = await accountRepository.GetVisibleByIdAsync(
                household.Id,
                membership.Id,
                accountId.Value,
                cancellationToken)
                ?? throw new NotFoundException("account", accountId.Value);

            if (!TransactionAccess.CanModify(account, membership))
                throw new ForbiddenException();
        }

        var labels = await labelRepository.GetApplicableAsync(
            household.Id,
            accountId,
            cancellationToken);

        return labels
            .Select(label => new LabelResult(
                label.Id,
                label.Name,
                label.Scope,
                label.AccountId))
            .ToArray();
    }
}

public sealed record LabelResult(
    Guid Id,
    string Name,
    LabelScope Scope,
    Guid? AccountId);

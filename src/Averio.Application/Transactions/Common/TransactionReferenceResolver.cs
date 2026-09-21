using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.Common;

public sealed class TransactionReferenceResolver(
    ICategoryRepository categoryRepository,
    ILabelRepository labelRepository)
{
    public async Task<IReadOnlyList<TransactionItemDraft>> ResolveItemsAsync(
        Guid householdId,
        TransactionKind kind,
        IReadOnlyList<TransactionItemInput> inputs,
        CancellationToken cancellationToken)
    {
        var ids = inputs.Select(item => item.CategoryId).Distinct().ToArray();
        var categories = await categoryRepository.GetAvailableByIdsAsync(
            householdId,
            ids,
            cancellationToken);

        if (categories.Count != ids.Length)
        {
            throw new BusinessRuleException(
                "transaction_category_invalid",
                "Validation_TransactionCategoryInvalid");
        }

        var byId = categories.ToDictionary(category => category.Id);

        if (categories.Any(category => !category.Supports(kind)))
        {
            throw new BusinessRuleException(
                "transaction_category_kind_invalid",
                "Validation_TransactionCategoryKindInvalid");
        }

        return inputs
            .Select(item =>
            {
                var category = byId[item.CategoryId];
                return new TransactionItemDraft(
                    item.Description,
                    category.Id,
                    category.NameReference,
                    category.Icon,
                    item.Amount);
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ResolveLabelIdsAsync(
        Guid householdId,
        Guid accountId,
        IReadOnlyList<Guid> labelIds,
        CancellationToken cancellationToken)
    {
        if (labelIds.Count == 0)
            return [];

        var labels = await labelRepository.GetApplicableByIdsAsync(
            householdId,
            accountId,
            labelIds,
            cancellationToken);

        if (labels.Count != labelIds.Count)
        {
            throw new BusinessRuleException(
                "transaction_label_invalid",
                "Validation_TransactionLabelInvalid");
        }

        return labelIds;
    }
}

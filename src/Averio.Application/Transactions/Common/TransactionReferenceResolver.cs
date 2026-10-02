using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Transactions;

namespace Averio.Application.Transactions.Common;

public sealed class TransactionReferenceResolver(ICategoryRepository categoryRepository, ILabelRepository labelRepository)
{
    public async Task<IReadOnlyList<TransactionItemDraft>> ResolveItemsAsync(
        Guid householdId, TransactionKind kind, IReadOnlyList<TransactionItemInput> inputs,
        CancellationToken cancellationToken, IReadOnlyCollection<TransactionItem>? existingItems = null)
    {
        var ids = inputs.Select(item => item.CategoryId).Distinct().ToArray();
        var categories = await categoryRepository.GetAvailableByIdsAsync(householdId, ids, cancellationToken);
        if (categories.Count != ids.Length)
            throw new BusinessRuleException("transaction_category_invalid", "Validation_TransactionCategoryInvalid");
        var byId = categories.ToDictionary(c => c.Id);
        var existing = (existingItems ?? []).ToDictionary(i => i.Id);
        var used = new HashSet<Guid>();

        return inputs.Select(item =>
        {
            var category = byId[item.CategoryId];
            TransactionItem? original = null;
            if (item.ExistingItemId is Guid itemId)
            {
                if (!used.Add(itemId) || !existing.TryGetValue(itemId, out original))
                    throw new BusinessRuleException("transaction_item_invalid", "Validation_TransactionExistingItem");
            }
            var retained = original?.CategoryId == item.CategoryId;
            if (category.IsArchived && !retained)
                throw new BusinessRuleException("transaction_category_archived", "Error_TransactionCategoryArchived");
            if (!category.Supports(kind))
                throw new BusinessRuleException("transaction_category_kind_invalid", "Validation_TransactionCategoryKindInvalid");
            return new TransactionItemDraft(item.Description, category.Id,
                retained ? original!.CategoryName : category.NameReference,
                retained ? original!.CategoryIcon : category.Icon, item.Amount);
        }).ToArray();
    }

    public async Task<IReadOnlyList<Guid>> ResolveLabelIdsAsync(
        Guid householdId, Guid accountId, IReadOnlyList<Guid> labelIds, CancellationToken cancellationToken)
    {
        if (labelIds.Count == 0) return [];
        var labels = await labelRepository.GetApplicableByIdsAsync(householdId, accountId, labelIds, cancellationToken);
        if (labels.Count != labelIds.Count)
            throw new BusinessRuleException("transaction_label_invalid", "Validation_TransactionLabelInvalid");
        return labelIds;
    }
}

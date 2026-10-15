using System.Text.Json;
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Application.Activities;
using Averio.Domain.Transactions;
namespace Averio.Application.Transactions.SavedViews;

public sealed class SavedViewHandler(ICurrentUser user, IHouseholdRepository households, IAccountRepository accounts,
    ILabelRepository labels, IActivityRepository activities, ISavedViewRepository views, IUnitOfWork uow, TimeProvider clock)
{
    private static readonly string[] Periods = ["this-month", "last-month", "previous-3-full-months", "this-year", "last-year", "last-12-months", "all", "custom"];
    public async Task<IReadOnlyList<SavedViewResult>> ListAsync(Guid householdId, CancellationToken ct)
    {
        var member = await Authorize(householdId, ct);
        var result = new List<SavedViewResult>();
        var visible = await accounts.GetVisibleAsync(householdId, member, true, ct);
        var options = await labels.GetForFilteringAsync(householdId, visible.Select(x => x.Id).ToArray(), ct);
        foreach (var view in await views.ListAsync(householdId, member, ct))
        {
            var filters = JsonSerializer.Deserialize<SavedViewFilters>(view.FiltersJson)!;
            var available = (filters.Account is null || visible.Any(x => x.Id == filters.Account))
                && filters.Labels.All(id => options.Any(x => x.Id == id))
                && (filters.Activity is null || await activities.GetAsync(householdId, member, filters.Activity.Value, ct) is not null);
            result.Add(new(view.Id, view.Name, filters, view.Version, available));
        }
        return result;
    }
    public async Task<Guid> SaveAsync(Guid householdId, Guid? id, Guid? expected, SavedViewInput input, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Authorize(householdId, ct);
        await views.LockMemberAsync(householdId, member, ct);
        // Recheck after acquiring the membership lock, including concurrent household departure.
        _ = await Authorize(householdId, ct);
        var f = input.Filters;
        if (string.IsNullOrWhiteSpace(input.Name) || input.Name.Trim().Length > 80 || f is null
            || !Periods.Contains(f.Period) || (f.Kind is not null && f.Kind is not "Expense" and not "Income")
            || f.Labels is null || f.Labels.Length > 20 || f.Labels.Any(x => x == Guid.Empty)
            || (f.Period == "custom" && f.From > f.To))
            throw new BusinessRuleException("saved_view_invalid", "Validation_SavedView");
        f = f with { From = f.Period == "custom" ? f.From : null, To = f.Period == "custom" ? f.To : null,
            Labels = f.Labels.Distinct().Order().ToArray() };
        if (!await Available(householdId, member, f, ct)) throw new BusinessRuleException("saved_view_unavailable", "Error_SavedViewUnavailable");
        var existing = await views.ListAsync(householdId, member, ct);
        if (existing.Any(x => x.Id != id && x.NormalizedName == input.Name.Trim().ToUpperInvariant()))
            throw new ConflictException("saved_view_duplicate", "Error_SavedViewDuplicate");
        SavedTransactionView view;
        if (id is Guid viewId)
        {
            view = await views.GetAsync(householdId, member, viewId, ct) ?? throw new NotFoundException("saved view", viewId);
            if (expected != view.Version) throw Changed();
            view.Update(input.Name, JsonSerializer.Serialize(f), clock.GetUtcNow());
        }
        else
        {
            if (existing.Count >= 50) throw new BusinessRuleException("saved_view_limit", "Error_SavedViewLimit");
            view = SavedTransactionView.Create(householdId, member, input.Name, JsonSerializer.Serialize(f), clock.GetUtcNow());
            views.Add(view);
        }
        await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct); return view.Id;
    }
    public async Task DeleteAsync(Guid householdId, Guid id, Guid expected, CancellationToken ct)
    {
        await using var tx = await uow.BeginTransactionAsync(ct);
        var member = await Authorize(householdId, ct);
        await views.LockMemberAsync(householdId, member, ct);
        _ = await Authorize(householdId, ct);
        var view = await views.GetAsync(householdId, member, id, ct) ?? throw new NotFoundException("saved view", id);
        if (view.Version != expected) throw Changed();
        views.Remove(view); await uow.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    private async Task<bool> Available(Guid householdId, Guid member, SavedViewFilters f, CancellationToken ct)
    {
        var visible = await accounts.GetVisibleAsync(householdId, member, true, ct);
        if (f.Account is Guid account && !visible.Any(x => x.Id == account)) return false;
        if (f.Activity is Guid activity && await activities.GetAsync(householdId, member, activity, ct) is null) return false;
        var options = await labels.GetForFilteringAsync(householdId, visible.Select(x => x.Id).ToArray(), ct);
        return f.Labels.All(id => options.Any(x => x.Id == id));
    }
    private async Task<Guid> Authorize(Guid householdId, CancellationToken ct)
    {
        var userId = user.UserId ?? throw new AuthenticationFailedException();
        var household = await households.GetByIdForUserAsync(householdId, userId, ct) ?? throw new NotFoundException("household", householdId);
        return household.Members.Single(x => x.UserId == userId).Id;
    }
    private static ConflictException Changed() => new("saved_view_changed", "Error_SavedViewChanged");
}

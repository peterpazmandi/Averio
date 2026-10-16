using Averio.Api.Mapping;
using Averio.Application.Recurring;
using Averio.Contracts.Recurring;
using Averio.Domain.Recurring;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Averio.Api.Controllers;

[Authorize, ApiController, Route("api/v1/households/{householdId:guid}/recurring")]
public sealed class RecurringController(RecurringHandler handler) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RecurringRuleResponse>>> List(Guid householdId, CancellationToken ct)
        => Ok((await handler.ListAsync(householdId, ct)).Select(x => { var r = x.Rule;
            return new RecurringRuleResponse(r.Id, r.AccountId, r.CategoryId, r.Name, DecimalAmountMapper.Format(r.Amount),
                x.Currency, r.Kind.ToString(), r.Visibility.ToString(), r.Frequency.ToString(), r.StartsOn, r.EndsOn,
                r.NextDueOn, r.Automatic, r.IsPaused, r.IsStopped, r.IsBlocked, x.CanRecord, r.Version, r.Items.OrderBy(i => i.Position).Select(i => new RecurringItemRequest(i.Description, i.CategoryId, DecimalAmountMapper.Format(i.Amount))).ToArray(),
                r.Labels.Select(l => l.LabelId).ToArray(), r.Merchant, r.Note, r.ActivityId); }).ToArray());
    [HttpPost]
    public async Task<ActionResult<CreateRecurringRuleResponse>> Create(Guid householdId, SaveRecurringRuleRequest request, CancellationToken ct)
        => StatusCode(201, new CreateRecurringRuleResponse(await handler.SaveAsync(householdId, null, null, Input(request), ct)));
    [HttpPut("{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid ruleId, SaveRecurringRuleRequest request, CancellationToken ct)
    {
        await handler.SaveAsync(householdId, ruleId, request.ExpectedVersion, Input(request), ct);
        return NoContent();
    }
    [HttpGet("{ruleId:guid}/occurrences")]
    public async Task<ActionResult<IReadOnlyList<RecurringOccurrenceResponse>>> History(Guid householdId, Guid ruleId, CancellationToken ct)
        => Ok((await handler.HistoryAsync(householdId, ruleId, ct)).Select(x => new RecurringOccurrenceResponse(x.DueOn, x.TransactionId, x.ProcessedAt)).ToArray());
    [HttpPost("{ruleId:guid}/occurrences")]
    public async Task<ActionResult<RecurringResolutionResponse>> Resolve(Guid householdId, Guid ruleId, RecurringOccurrenceRequest request, CancellationToken ct)
        => Ok(new RecurringResolutionResponse(await handler.ResolveAsync(householdId, ruleId, request.ExpectedVersion,
            request.DueOn, request.Skip, request.Amount is null ? null : DecimalAmountMapper.ParseTransactionAmount(request.Amount), ct, request.ItemAmounts?.Select(DecimalAmountMapper.ParseTransactionAmount).ToArray())));
    private static RecurringInput Input(SaveRecurringRuleRequest r) => new(r.AccountId, r.CategoryId, r.Name,
        DecimalAmountMapper.ParseTransactionAmount(r.Amount),
        EnumParser.Parse<TransactionKind>(r.Kind, nameof(r.Kind), "Validation_TransactionKindInvalid"),
        EnumParser.Parse<TransactionVisibility>(r.Visibility, nameof(r.Visibility), "Validation_TransactionVisibilityInvalid"),
        EnumParser.Parse<RecurringFrequency>(r.Frequency, nameof(r.Frequency), "Validation_Recurring"),
        r.StartsOn, r.EndsOn, r.Automatic, r.IsPaused, r.IsStopped,
        r.Items?.Select(i => new Averio.Application.Transactions.Common.TransactionItemInput(i.Description, i.CategoryId, DecimalAmountMapper.ParseTransactionAmount(i.Amount))).ToArray(),
        r.LabelIds, r.Merchant, r.Note, r.ActivityId);
}

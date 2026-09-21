using Averio.Api.Mapping;
using Averio.Application.Categories.GetCategories;
using Averio.Contracts.Categories;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/categories")]
public sealed class CategoriesController(GetCategoriesHandler handler)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        Guid householdId,
        [FromQuery] string? kind,
        CancellationToken cancellationToken)
    {
        TransactionKind? parsedKind = string.IsNullOrWhiteSpace(kind)
            ? null
            : EnumParser.Parse<TransactionKind>(
                kind,
                nameof(kind),
                "Validation_TransactionKindInvalid");

        var result = await handler.HandleAsync(
            householdId,
            parsedKind,
            cancellationToken);

        return Ok(result.Select(category => new CategoryResponse(
            category.Id,
            category.ParentId,
            category.Code,
            category.Name,
            category.Kind.ToString(),
            category.Icon,
            category.AccentColor,
            category.SortOrder,
            category.IsBase)));
    }
}

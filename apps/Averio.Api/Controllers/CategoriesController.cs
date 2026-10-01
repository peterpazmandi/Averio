using Averio.Api.Mapping;
using Averio.Application.Categories.GetCategories;
using Averio.Application.Categories.ManageCategories;
using Averio.Contracts.Categories;
using Averio.Domain.Categories;
using Averio.Domain.Transactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/categories")]
public sealed class CategoriesController(GetCategoriesHandler reader, CategoryManagementHandler writer) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CategoryResponse>>> List(
        Guid householdId, [FromQuery] string? kind, CancellationToken cancellationToken,
        [FromQuery] bool includeArchived = false)
    {
        TransactionKind? parsedKind = string.IsNullOrWhiteSpace(kind) ? null
            : EnumParser.Parse<TransactionKind>(kind, nameof(kind), "Validation_TransactionKindInvalid");
        var result = await reader.HandleAsync(householdId, parsedKind, cancellationToken, includeArchived);
        return Ok(result.Select(c => new CategoryResponse(c.Id, c.ParentId, c.SystemKey, c.Name,
            c.Kind.ToString(), c.Icon, c.AccentColor, c.SortOrder, c.IsSystem, c.IsArchived, c.UpdatedAt)));
    }

    [HttpPost]
    public async Task<ActionResult<CategoryCreatedResponse>> Create(
        Guid householdId, CreateCategoryRequest request, CancellationToken ct)
    {
        var id = await writer.CreateAsync(householdId,
            Input(request.Name, request.Kind, request.Icon, request.AccentColor, request.ParentId), ct);
        return StatusCode(StatusCodes.Status201Created, new CategoryCreatedResponse(id));
    }

    [HttpPut("{categoryId:guid}")]
    public async Task<IActionResult> Update(Guid householdId, Guid categoryId,
        UpdateCategoryRequest request, CancellationToken ct)
    {
        await writer.UpdateAsync(householdId, categoryId,
            Input(request.Name, request.Kind, request.Icon, request.AccentColor, request.ParentId),
            request.ExpectedUpdatedAt, ct);
        return NoContent();
    }

    [HttpPut("{categoryId:guid}/archive")]
    public async Task<IActionResult> SetArchive(Guid householdId, Guid categoryId,
        CategoryArchiveRequest request, CancellationToken ct)
    {
        await writer.SetArchivedAsync(householdId, categoryId, request.IsArchived, request.ExpectedUpdatedAt, ct);
        return NoContent();
    }

    private static CategoryInput Input(string name, string kind, string icon, string color, Guid? parent)
        => new(name, EnumParser.Parse<CategoryKind>(kind, nameof(kind), "Validation_CategoryKind"), icon, color, parent);
}

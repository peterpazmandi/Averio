using Averio.Application.Accounts.ArchiveAccount;
using Averio.Application.Accounts.RestoreAccount;
using Averio.Api.Mapping;
using Averio.Application.Accounts.CreateAccount;
using Averio.Application.Accounts.GetAccount;
using Averio.Application.Accounts.GetAccounts;
using Averio.Contracts.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/v1/households/{householdId:guid}/accounts")]
public sealed class AccountsController(
    CreateAccountHandler createHandler,
    GetAccountHandler getHandler,
    GetAccountsHandler listHandler,
    ArchiveAccountHandler archiveHandler,
    RestoreAccountHandler restoreHandler,
    Averio.Application.Accounts.UpdateAccount.UpdateAccountHandler updateHandler)
    : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateAccountResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateAccountResponse>> Create(
        Guid householdId,
        CreateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var command = AccountRequestMapper.ToCommand(
            householdId,
            request);

        var accountId = await createHandler.HandleAsync(
            command,
            cancellationToken);

        return CreatedAtAction(
            nameof(Get),
            new
            {
                householdId,
                accountId
            },
            new CreateAccountResponse(accountId));
    }

    [HttpPut("{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid householdId,
        Guid accountId,
        UpdateAccountRequest request,
        CancellationToken cancellationToken)
    {
        await updateHandler.HandleAsync(AccountRequestMapper.ToCommand(
            householdId, accountId, request), cancellationToken);
        return NoContent();
    }

    [HttpGet("{accountId:guid}")]
    public async Task<ActionResult<AccountResponse>> Get(
        Guid householdId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        var result = await getHandler.HandleAsync(
            householdId,
            accountId,
            cancellationToken);

        return Ok(AccountResponseMapper.From(result));
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResponse>>> GetAll(
        Guid householdId,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken)
    {
        var results = await listHandler.HandleAsync(
            householdId,
            includeArchived,
            cancellationToken);

        return Ok(AccountResponseMapper.From(results));
    }

    [HttpPut("{accountId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(
        Guid householdId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await archiveHandler.HandleAsync(
            householdId,
            accountId,
            cancellationToken);

        return NoContent();
    }

    [HttpDelete("{accountId:guid}/archive")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Restore(
        Guid householdId,
        Guid accountId,
        CancellationToken cancellationToken)
    {
        await restoreHandler.HandleAsync(
            householdId,
            accountId,
            cancellationToken);

        return NoContent();
    }
}

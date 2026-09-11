using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
    GetAccountsHandler listHandler)
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
}
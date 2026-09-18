using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Averio.Application.Accounts.Common;

namespace Averio.Application.Accounts.GetAccountOwnerOptions;

public sealed record AccountOwnerOptionsResult(
    Guid CurrentMemberId,
    IReadOnlyList<AccountOwnerResult> Members);
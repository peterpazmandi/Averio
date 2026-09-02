using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.VerifyEmail
{
    public sealed record VerifyEmailCommand(
        Guid UserId,
        string Token);

}
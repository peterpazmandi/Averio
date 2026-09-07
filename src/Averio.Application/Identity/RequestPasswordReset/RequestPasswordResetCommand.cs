using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.RequestPasswordReset
{
    public sealed record RequestPasswordResetCommand(string Email);
}
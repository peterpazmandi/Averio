using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.ResetPassword
{
    public sealed record ResetPasswordCommand(
        Guid UserId,
        string Token,
        string Password);
}
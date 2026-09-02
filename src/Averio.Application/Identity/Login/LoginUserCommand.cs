using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.Login
{
    public sealed record LoginUserCommand(string Email, string Password, bool RememberMe);
}
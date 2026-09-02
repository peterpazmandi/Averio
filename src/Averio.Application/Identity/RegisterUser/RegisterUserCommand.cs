using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.RegisterUser
{
    public sealed record RegisterUserCommand(string Email, string Password, string Culture);
}
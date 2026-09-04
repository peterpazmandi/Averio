using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Averio.Application.Identity.ResendVerification
{
    public sealed record ResendEmailVerificationCommand(string Email);
}
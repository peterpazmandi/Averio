using System.Security.Claims;
using Averio.Application.Common.Abstractions;

namespace Averio.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value =
                httpContextAccessor
                    .HttpContext?
                    .User
                    .FindFirstValue(
                        ClaimTypes.NameIdentifier);

            return Guid.TryParse(
                value,
                out var userId)
                ? userId
                : null;
        }
    }
}
using Averio.Domain.Households;
using Microsoft.AspNetCore.Identity;

namespace Averio.Infrastructure.Identity;

public sealed class ApplicationUser
    : IdentityUser<Guid>
{
    public string? DisplayName { get; set; }

    public string PreferredCulture { get; set; } =
        "en-US";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }


}

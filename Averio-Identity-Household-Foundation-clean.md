# Averio — Identity + Household Foundation

Yes. This is the point where I would stop thinking of authentication as a peripheral feature and make it part of Averio's permanent foundation.

The most important invariant is:

```text
Every registered User
        │
        ▼
has exactly one active HouseholdMember
        │
        ▼
belonging to exactly one Household
```

That directly implements Averio's existing “Household of One” concept: registration creates the user and their initial household together, so later “joining a partner” becomes a household merge/membership operation rather than retrofitting household support onto solo users.

There is one change from the older Averio architecture I recommend making during this restart. Your description specifies MediatR, but current MediatR 13+ is commercially licensed or available under the reciprocal RPL license; current MediatR is 14.2.0. Since Averio is intended to become a commercial SaaS, I would not introduce that licensing dependency into the new codebase. We can keep the vertical-slice/mediator architecture with explicit handlers registered in DI. FluentValidation remains Apache-2.0 and is currently 12.1.1.

So we're going to have thin controllers like before, but instead of:

```text
Controller
    ↓
MediatR
    ↓
Handler
```

we'll have:

```text
Controller
    ↓
Handler
```

Everything else about the architecture stays the same.

## 1. What we're implementing

After this step Averio will support:

```text
Registration
    │
    ├── create Identity user
    ├── hash password
    ├── create Household
    ├── create Owner HouseholdMember
    └── send verification email
              │
              ▼
       Email verification
              │
              ▼
            Login
              │
              ▼
       HttpOnly auth cookie
              │
              ▼
         GET /auth/me
              │
              ▼
User → HouseholdMember → Household
```

And we're also adding:

- ✓ password hashing through ASP.NET Core Identity
- ✓ DB-level unique email
- ✓ email verification
- ✓ 24h verification tokens
- ✓ persistent Data Protection keys
- ✓ cookie authentication
- ✓ CSRF protection
- ✓ account lockout
- ✓ localized errors
- ✓ localized verification emails
- ✓ Brevo transactional email support
- ✓ development email logger
- ✓ one household per user
- ✓ exactly one owner per household

ASP.NET Core Identity is a good fit here because it already provides secure password storage, email confirmation, lockout, claims, external-logins support and cookie integration.

## 2. Identity roles are NOT household roles

This distinction is important.

I don't want this:

ASP.NET Identity role
    Owner
    Member

because those roles are global.

An Averio user is not globally an “Owner”.

They are:

```text
User
  │
  └── HouseholdMember
          │
          ├── Household = X
          └── Role = Owner
```

Therefore we're deliberately using Identity without ASP.NET Roles.

That means no:

roles
user_roles
role_claims

tables.

Household authorization remains part of Averio's domain.

## 3. The resulting database model

Conceptually:

```text
users
  │
  │ 1
  │
  ▼
household_members
  │
  │ *
  ▼
households
```

More precisely:

```text
users
│
├── user_claims
├── user_logins
├── user_tokens
│
└── household_members
        │
        └── households
```

data_protection_keys

The important constraint is:

household_members.user_id UNIQUE

Therefore currently:

one Averio user belongs to exactly one household.

That matches the Household-of-One model.

Later when Péter and Partner combine households:

Peter household              Partner household

```text
       │                             │
       └────────── merge ────────────┘
                     │
                     ▼
              Shared household
                │          │
              Peter      Partner
              Owner       Member
```

we change the membership and move financial data transactionally.

## 4. Update NuGet versions

Add these to Directory.Packages.props:

```xml
<PackageVersion Include="FluentValidation"
                Version="12.1.1" />

<PackageVersion Include="FluentValidation.DependencyInjectionExtensions"
                Version="12.1.1" />

<PackageVersion Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore"
                Version="10.0.11" />

<PackageVersion Include="Microsoft.AspNetCore.DataProtection.EntityFrameworkCore"
                Version="10.0.11" />

<PackageVersion Include="Microsoft.Extensions.Http"
                Version="10.0.11" />

<PackageVersion Include="Microsoft.Extensions.Localization.Abstractions"
                Version="10.0.11" />

<PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions"
                Version="10.0.11" />
```

## 5. Update Averio.Application.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Averio.Application</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="FluentValidation" />
    <PackageReference Include="FluentValidation.DependencyInjectionExtensions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\Averio.Domain\\Averio.Domain.csproj" />
  </ItemGroup>

</Project>
```

## 6. Update Averio.Infrastructure.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>Averio.Infrastructure</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.EntityFrameworkCore" />

    <PackageReference Include="Microsoft.EntityFrameworkCore.Design">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>
        runtime;
        build;
        native;
        contentfiles;
        analyzers;
        buildtransitive
      </IncludeAssets>
    </PackageReference>

    <PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" />
    <PackageReference Include="Microsoft.AspNetCore.DataProtection.EntityFrameworkCore" />

    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Http" />
    <PackageReference Include="Microsoft.Extensions.Localization.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />

    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\\Averio.Application\\Averio.Application.csproj" />
    <ProjectReference Include="..\\Averio.Domain\\Averio.Domain.csproj" />
  </ItemGroup>

</Project>
```

## 7. New folder structure

```text
src/
├── Averio.Domain/
│   └── Households/
│       ├── Household.cs
│       ├── HouseholdMember.cs
│       └── HouseholdRole.cs
│
├── Averio.Application/
│   ├── Common/
│   │   ├── Abstractions/
│   │   │   ├── CurrentUser.cs
│   │   │   └── Persistence.cs
│   │   └── Exceptions/
│   │       └── AverioException.cs
│   │
│   ├── Identity/
│   │   ├── IdentityAbstractions.cs
│   │   ├── RegisterUser/
│   │   │   └── RegisterUser.cs
│   │   ├── Login/
│   │   │   └── LoginUser.cs
│   │   ├── VerifyEmail/
│   │   │   └── VerifyEmail.cs
│   │   ├── ResendVerification/
│   │   │   └── ResendEmailVerification.cs
│   │   └── GetCurrentUser/
│   │       └── GetCurrentUser.cs
│   │
│   └── DependencyInjection.cs
│
├── Averio.Contracts/
│   ├── Authentication/
│   │   └── AuthenticationContracts.cs
│   └── Security/
│       └── CsrfTokenResponse.cs
│
└── Averio.Infrastructure/
    ├── Identity/
    │   ├── ApplicationUser.cs
    │   ├── IdentityService.cs
    │   └── UserSessionService.cs
    │
    ├── Persistence/
    │   ├── AverioDbContext.cs
    │   ├── UnitOfWork.cs
    │   ├── Repositories/
    │   │   └── HouseholdRepository.cs
    │   └── Configurations/
    │       ├── IdentityEntityConfigurations.cs
    │       ├── HouseholdConfiguration.cs
    │       └── DataProtectionKeyConfiguration.cs
    │
    ├── Email/
    │   ├── EmailOptions.cs
    │   ├── ITransactionalEmailSender.cs
    │   ├── DevelopmentTransactionalEmailSender.cs
    │   ├── BrevoTransactionalEmailSender.cs
    │   └── EmailVerificationSender.cs
    │
    ├── Resources/
    │   ├── EmailMessages.en-US.resx
    │   ├── EmailMessages.de-DE.resx
    │   └── EmailMessages.hu-HU.resx
    │
    ├── EmailMessages.cs
    └── DependencyInjection.cs
```

API additions:

```text
apps/Averio.Api/
├── Authentication/
│   └── CurrentUser.cs
├── Security/
│   └── CsrfProtectionMiddleware.cs
├── Controllers/
│   ├── AuthController.cs
│   └── SecurityController.cs
└── ...
```

## 8. Household role

src/Averio.Domain/Households/HouseholdRole.cs

```csharp
namespace Averio.Domain.Households;

public enum HouseholdRole
{
    Owner = 1,
    Member = 2
}
```

Start with just Owner and Member. Later authorization permissions should be separate from these coarse roles.

## 9. Household member

src/Averio.Domain/Households/HouseholdMember.cs

```csharp
namespace Averio.Domain.Households;

public sealed class HouseholdMember
{
    private HouseholdMember()
    {
    }

    private HouseholdMember(
        Guid id,
        Guid householdId,
        Guid userId,
        HouseholdRole role,
        DateTimeOffset joinedAtUtc)
    {
        Id = id;
        HouseholdId = householdId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid HouseholdId { get; private set; }

    public Guid UserId { get; private set; }

    public HouseholdRole Role { get; private set; }

    public DateTimeOffset JoinedAtUtc { get; private set; }

    internal static HouseholdMember CreateOwner(
        Guid householdId,
        Guid userId,
        DateTimeOffset joinedAtUtc)
    {
        return new HouseholdMember(
            Guid.NewGuid(),
            householdId,
            userId,
            HouseholdRole.Owner,
            joinedAtUtc);
    }
}
```

## 10. Household aggregate

src/Averio.Domain/Households/Household.cs

```csharp
namespace Averio.Domain.Households;

public sealed class Household
{
    private readonly List<HouseholdMember> _members = [];

    private Household()
    {
    }

    private Household(
        Guid id,
        DateTimeOffset createdAtUtc)
    {
        Id = id;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string? Name { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyCollection<HouseholdMember> Members =>
        _members;

    public static Household CreateForUser(
        Guid userId,
        DateTimeOffset now)
    {
        var household = new Household(
            Guid.NewGuid(),
            now);

        household._members.Add(
            HouseholdMember.CreateOwner(
                household.Id,
                userId,
                now));

        return household;
    }
}
```

Name is deliberately nullable. Registration should not invent a household name; the UI can display a localized placeholder until onboarding assigns a real name.

## 11. Update application exceptions

```csharp
namespace Averio.Application.Common.Exceptions;

public abstract class AverioException : Exception
{
    protected AverioException(
        string code,
        string resourceKey,
        params object[] arguments)
        : base(resourceKey)
    {
        Code = code;
        ResourceKey = resourceKey;
        Arguments = arguments;
    }

    public string Code { get; }
    public string ResourceKey { get; }
    public object[] Arguments { get; }
}

public sealed class NotFoundException : AverioException
{
    public NotFoundException(string resourceName, object identifier)
        : base("resource_not_found", ErrorResourceKeys.NotFound, resourceName, identifier)
    {
    }
}

public sealed class ConflictException : AverioException
{
    public ConflictException(string code, string resourceKey, params object[] arguments)
        : base(code, resourceKey, arguments)
    {
    }
}

public sealed class BusinessRuleException : AverioException
{
    public BusinessRuleException(string code, string resourceKey, params object[] arguments)
        : base(code, resourceKey, arguments)
    {
    }
}

public sealed class AuthenticationFailedException : AverioException
{
    public AuthenticationFailedException()
        : base("invalid_credentials", ErrorResourceKeys.InvalidCredentials)
    {
    }
}

public sealed class EmailNotVerifiedException : AverioException
{
    public EmailNotVerifiedException()
        : base("email_not_verified", ErrorResourceKeys.EmailNotVerified)
    {
    }
}

public sealed class AccountLockedException : AverioException
{
    public AccountLockedException()
        : base("account_locked", ErrorResourceKeys.AccountLocked)
    {
    }
}

public sealed class InvalidEmailVerificationTokenException : AverioException
{
    public InvalidEmailVerificationTokenException()
        : base("invalid_email_verification_token", ErrorResourceKeys.InvalidEmailVerificationToken)
    {
    }
}

public sealed class HouseholdRequiredException : AverioException
{
    public HouseholdRequiredException()
        : base("household_required", ErrorResourceKeys.HouseholdRequired)
    {
    }
}

public static class ErrorResourceKeys
{
    public const string NotFound = "Error_NotFound";
    public const string BusinessRule = "Error_BusinessRule";
    public const string InvalidCredentials = "Error_InvalidCredentials";
    public const string EmailNotVerified = "Error_EmailNotVerified";
    public const string AccountLocked = "Error_AccountLocked";
    public const string InvalidEmailVerificationToken = "Error_InvalidEmailVerificationToken";
    public const string EmailAlreadyRegistered = "Error_EmailAlreadyRegistered";
    public const string HouseholdRequired = "Error_HouseholdRequired";
    public const string IdentityCreationFailed = "Error_IdentityCreationFailed";
}
```

## 12. Persistence abstractions

src/Averio.Application/Common/Abstractions/Persistence.cs

```csharp
using Averio.Domain.Households;

namespace Averio.Application.Common.Abstractions;

public interface IUnitOfWork
{
    Task<IApplicationTransaction> BeginTransactionAsync(CancellationToken cancellationToken);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IApplicationTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}

public interface IHouseholdRepository
{
    void Add(Household household);

    Task<HouseholdMember?> GetMembershipByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken);
}
```

## 13. Current-user abstraction

```csharp
namespace Averio.Application.Common.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
}
```

Application knows nothing about HttpContext, claims, or cookies.

## 14. Identity abstractions

```csharp
namespace Averio.Application.Identity;

public interface IIdentityService
{
    Task<IdentityUserInfo?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<IdentityUserInfo?> FindByIdAsync(Guid userId, CancellationToken cancellationToken);

    Task<IdentityUserCreationResult> CreateUserAsync(
        string email,
        string password,
        string preferredCulture,
        CancellationToken cancellationToken);

    Task<string?> GenerateEmailVerificationTokenAsync(Guid userId, CancellationToken cancellationToken);

    Task<EmailConfirmationStatus> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken);
}

public interface IUserSessionService
{
    Task<SignInStatus> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);
}

public interface IEmailVerificationSender
{
    Task<bool> TrySendAsync(
        Guid userId,
        string email,
        string verificationToken,
        string culture,
        CancellationToken cancellationToken);
}

public sealed record IdentityUserInfo(
    Guid Id,
    string Email,
    bool EmailConfirmed,
    string PreferredCulture);

public sealed record IdentityUserCreationResult(
    bool Succeeded,
    Guid? UserId,
    IReadOnlyCollection<string> ErrorCodes);

public enum EmailConfirmationStatus
{
    Succeeded,
    AlreadyConfirmed,
    UserNotFound,
    InvalidToken
}

public enum SignInStatus
{
    Succeeded,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut
}

public static class IdentityCreationErrorCodes
{
    public const string DuplicateEmail = "duplicate_email";
    public const string Rejected = "identity_rejected";
}
```

## 15. Registration slice

src/Averio.Application/Identity/RegisterUser/RegisterUser.cs

```csharp
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Households;
using FluentValidation;

namespace Averio.Application.Identity.RegisterUser;

public sealed record RegisterUserCommand(
    string Email,
    string Password,
    string Culture);

public sealed record RegisterUserResult(
    Guid UserId,
    bool VerificationEmailSent);

public sealed class RegisterUserValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("Validation_EmailRequired")
            .EmailAddress().WithErrorCode("Validation_EmailInvalid")
            .MaximumLength(320).WithErrorCode("Validation_EmailTooLong");

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode("Validation_PasswordRequired")
            .MinimumLength(12).WithErrorCode("Validation_PasswordTooShort")
            .MaximumLength(128).WithErrorCode("Validation_PasswordTooLong");
    }
}

public sealed class RegisterUserHandler(
    IValidator<RegisterUserCommand> validator,
    IIdentityService identityService,
    IEmailVerificationSender emailVerificationSender,
    IHouseholdRepository householdRepository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
{
    public async Task<RegisterUserResult> HandleAsync(
        RegisterUserCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var normalizedEmail = command.Email.Trim();

        var existingUser = await identityService.FindByEmailAsync(
            normalizedEmail,
            cancellationToken);

        if (existingUser is not null)
        {
            throw new ConflictException(
                "email_already_registered",
                ErrorResourceKeys.EmailAlreadyRegistered);
        }

        await using var transaction =
            await unitOfWork.BeginTransactionAsync(cancellationToken);

        var creationResult = await identityService.CreateUserAsync(
            normalizedEmail,
            command.Password,
            command.Culture,
            cancellationToken);

        if (!creationResult.Succeeded || creationResult.UserId is null)
        {
            if (creationResult.ErrorCodes.Contains(IdentityCreationErrorCodes.DuplicateEmail))
            {
                throw new ConflictException(
                    "email_already_registered",
                    ErrorResourceKeys.EmailAlreadyRegistered);
            }

            throw new BusinessRuleException(
                "identity_creation_failed",
                ErrorResourceKeys.IdentityCreationFailed);
        }

        var userId = creationResult.UserId.Value;

        var household = Household.CreateForUser(
            userId,
            timeProvider.GetUtcNow());

        householdRepository.Add(household);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        // Email is sent AFTER the database transaction commits.
        // Do not keep a DB transaction open while calling Brevo.

        var verificationToken = await identityService
            .GenerateEmailVerificationTokenAsync(userId, cancellationToken);

        var emailSent = verificationToken is not null
            && await emailVerificationSender.TrySendAsync(
                userId,
                normalizedEmail,
                verificationToken,
                command.Culture,
                cancellationToken);

        return new RegisterUserResult(userId, emailSent);
    }
}
```

User + Household + Owner membership is transactional. Brevo is not.

## 16. Login slice

```csharp
using Averio.Application.Common.Exceptions;
using FluentValidation;

namespace Averio.Application.Identity.Login;

public sealed record LoginUserCommand(
    string Email,
    string Password,
    bool RememberMe);

public sealed class LoginUserValidator : AbstractValidator<LoginUserCommand>
{
    public LoginUserValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("Validation_EmailRequired")
            .EmailAddress().WithErrorCode("Validation_EmailInvalid");

        RuleFor(x => x.Password)
            .NotEmpty().WithErrorCode("Validation_PasswordRequired");
    }
}

public sealed class LoginUserHandler(
    IValidator<LoginUserCommand> validator,
    IUserSessionService userSessionService)
{
    public async Task HandleAsync(
        LoginUserCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var result = await userSessionService.SignInAsync(
            command.Email.Trim(),
            command.Password,
            command.RememberMe,
            cancellationToken);

        switch (result)
        {
            case SignInStatus.Succeeded:
                return;

            case SignInStatus.EmailNotConfirmed:
                throw new EmailNotVerifiedException();

            case SignInStatus.LockedOut:
                throw new AccountLockedException();

            default:
                throw new AuthenticationFailedException();
        }
    }
}
```

## 17. Verify-email slice

```csharp
using Averio.Application.Common.Exceptions;
using FluentValidation;

namespace Averio.Application.Identity.VerifyEmail;

public sealed record VerifyEmailCommand(Guid UserId, string Token);

public sealed class VerifyEmailValidator : AbstractValidator<VerifyEmailCommand>
{
    public VerifyEmailValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithErrorCode("Validation_UserIdRequired");

        RuleFor(x => x.Token)
            .NotEmpty().WithErrorCode("Validation_TokenRequired");
    }
}

public sealed class VerifyEmailHandler(
    IValidator<VerifyEmailCommand> validator,
    IIdentityService identityService)
{
    public async Task HandleAsync(
        VerifyEmailCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var status = await identityService.ConfirmEmailAsync(
            command.UserId,
            command.Token,
            cancellationToken);

        switch (status)
        {
            case EmailConfirmationStatus.Succeeded:
            case EmailConfirmationStatus.AlreadyConfirmed:
                return;

            default:
                throw new InvalidEmailVerificationTokenException();
        }
    }
}
```

Verification is idempotent: already verified still returns success.

## 18. Resend verification slice

```csharp
using FluentValidation;

namespace Averio.Application.Identity.ResendVerification;

public sealed record ResendEmailVerificationCommand(string Email);

public sealed class ResendEmailVerificationValidator
    : AbstractValidator<ResendEmailVerificationCommand>
{
    public ResendEmailVerificationValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrorCode("Validation_EmailRequired")
            .EmailAddress().WithErrorCode("Validation_EmailInvalid")
            .MaximumLength(320).WithErrorCode("Validation_EmailTooLong");
    }
}

public sealed class ResendEmailVerificationHandler(
    IValidator<ResendEmailVerificationCommand> validator,
    IIdentityService identityService,
    IEmailVerificationSender emailVerificationSender)
{
    public async Task HandleAsync(
        ResendEmailVerificationCommand command,
        CancellationToken cancellationToken)
    {
        await validator.ValidateAndThrowAsync(command, cancellationToken);

        var user = await identityService.FindByEmailAsync(
            command.Email.Trim(),
            cancellationToken);

        // Don't disclose whether the email exists.
        if (user is null || user.EmailConfirmed)
        {
            return;
        }

        var token = await identityService
            .GenerateEmailVerificationTokenAsync(user.Id, cancellationToken);

        if (token is null)
        {
            return;
        }

        await emailVerificationSender.TrySendAsync(
            user.Id,
            user.Email,
            token,
            user.PreferredCulture,
            cancellationToken);
    }
}
```

The client always receives success, preventing account enumeration.

## 19. Current-user slice

```csharp
using Averio.Application.Common.Abstractions;
using Averio.Application.Common.Exceptions;
using Averio.Domain.Households;

namespace Averio.Application.Identity.GetCurrentUser;

public sealed record CurrentUserResult(
    Guid UserId,
    string Email,
    bool EmailVerified,
    Guid HouseholdId,
    HouseholdRole HouseholdRole);

public sealed class GetCurrentUserHandler(
    ICurrentUser currentUser,
    IIdentityService identityService,
    IHouseholdRepository householdRepository)
{
    public async Task<CurrentUserResult> HandleAsync(
        CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId
            ?? throw new AuthenticationFailedException();

        var user = await identityService.FindByIdAsync(
            userId,
            cancellationToken)
            ?? throw new AuthenticationFailedException();

        var membership = await householdRepository
            .GetMembershipByUserIdAsync(userId, cancellationToken)
            ?? throw new HouseholdRequiredException();

        return new CurrentUserResult(
            user.Id,
            user.Email,
            user.EmailConfirmed,
            membership.HouseholdId,
            membership.Role);
    }
}
```

## 20. Application DI

```csharp
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Averio.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<VerifyEmailHandler>();
        services.AddScoped<ResendEmailVerificationHandler>();
        services.AddScoped<GetCurrentUserHandler>();

        return services;
    }
}
```

## 21. ApplicationUser

src/Averio.Infrastructure/Identity/ApplicationUser.cs

```csharp
using Averio.Domain.Households;
using Microsoft.AspNetCore.Identity;

namespace Averio.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string PreferredCulture { get; set; } = "en-US";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? LastLoginAtUtc { get; set; }

    public HouseholdMember? HouseholdMembership
    {
        get;
        private set;
    }
}
```

ApplicationUser belongs in Infrastructure, not Domain. The business model should not know about PasswordHash, SecurityStamp or LockoutEnd.

## 22. Update DbContext

```csharp
using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence;

public sealed class AverioDbContext(
    DbContextOptions<AverioDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options),
      IDataProtectionKeyContext
{
    public DbSet<Household> Households => Set<Household>();

    public DbSet<HouseholdMember> HouseholdMembers => Set<HouseholdMember>();

    public DbSet<DataProtectionKey> DataProtectionKeys
    {
        get;
        set;
    } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AverioDbContext).Assembly);
    }
}
```

## 23. Why Data Protection keys are in PostgreSQL

ASP.NET Core uses Data Protection for authentication cookies, email verification tokens and password-reset tokens.

If those keys only live inside your Docker container:

```text
container restart
      ↓
new keys
      ↓
existing auth cookies invalid
verification links may become invalid
```

Persisting them in PostgreSQL means current and future containers can share the same key ring.

## 24. Configure the user table

Persistence/Configurations/IdentityEntityConfigurations.cs

```csharp
using Averio.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class ApplicationUserConfiguration
    : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id).HasName("pk_users");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserName).HasColumnName("user_name").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Email).HasColumnName("email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
        builder.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
        builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        builder.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasMaxLength(32);
        builder.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
        builder.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
        builder.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
        builder.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
        builder.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
        builder.Property(x => x.PreferredCulture).HasColumnName("preferred_culture").HasMaxLength(10).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc");

        builder.HasIndex(x => x.NormalizedUserName)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_user_name");

        builder.HasIndex(x => x.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ux_users_normalized_email");
    }
}

internal sealed class IdentityUserClaimConfiguration
    : IEntityTypeConfiguration<IdentityUserClaim<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserClaim<Guid>> builder)
    {
        builder.ToTable("user_claims");
        builder.HasKey(x => x.Id).HasName("pk_user_claims");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.ClaimType).HasColumnName("claim_type");
        builder.Property(x => x.ClaimValue).HasColumnName("claim_value");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_claims_user_id");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_claims_users_user_id");
    }
}

internal sealed class IdentityUserLoginConfiguration
    : IEntityTypeConfiguration<IdentityUserLogin<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserLogin<Guid>> builder)
    {
        builder.ToTable("user_logins");

        builder.HasKey(x => new { x.LoginProvider, x.ProviderKey })
            .HasName("pk_user_logins");

        builder.Property(x => x.LoginProvider)
            .HasColumnName("login_provider")
            .HasMaxLength(128);

        builder.Property(x => x.ProviderKey)
            .HasColumnName("provider_key")
            .HasMaxLength(128);

        builder.Property(x => x.ProviderDisplayName)
            .HasColumnName("provider_display_name");

        builder.Property(x => x.UserId)
            .HasColumnName("user_id");

        builder.HasIndex(x => x.UserId)
            .HasDatabaseName("ix_user_logins_user_id");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_logins_users_user_id");
    }
}

internal sealed class IdentityUserTokenConfiguration
    : IEntityTypeConfiguration<IdentityUserToken<Guid>>
{
    public void Configure(EntityTypeBuilder<IdentityUserToken<Guid>> builder)
    {
        builder.ToTable("user_tokens");

        builder.HasKey(x => new { x.UserId, x.LoginProvider, x.Name })
            .HasName("pk_user_tokens");

        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.LoginProvider).HasColumnName("login_provider").HasMaxLength(128);
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(128);
        builder.Property(x => x.Value).HasColumnName("value");

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_user_tokens_users_user_id");
    }
}
```

The user_logins table is useful later for Google, Facebook, Apple, etc. without changing the core user model.

## 25. Household EF configuration

```csharp
using Averio.Domain.Households;
using Averio.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class HouseholdConfiguration : IEntityTypeConfiguration<Household>
{
    public void Configure(EntityTypeBuilder<Household> builder)
    {
        builder.ToTable("households");
        builder.HasKey(x => x.Id).HasName("pk_households");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(120);
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.Navigation(x => x.Members)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class HouseholdMemberConfiguration
    : IEntityTypeConfiguration<HouseholdMember>
{
    public void Configure(EntityTypeBuilder<HouseholdMember> builder)
    {
        builder.ToTable(
            "household_members",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_household_members_role",
                    "role IN ('owner', 'member')");
            });

        builder.HasKey(x => x.Id).HasName("pk_household_members");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.HouseholdId).HasColumnName("household_id");
        builder.Property(x => x.UserId).HasColumnName("user_id");

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasMaxLength(16)
            .HasConversion(
                role => role.ToString().ToLowerInvariant(),
                value => Enum.Parse<HouseholdRole>(value, ignoreCase: true));

        builder.Property(x => x.JoinedAtUtc)
            .HasColumnName("joined_at_utc")
            .IsRequired();

        builder.HasOne<Household>()
            .WithMany(x => x.Members)
            .HasForeignKey(x => x.HouseholdId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_household_members_households_household_id");

        builder.HasOne<ApplicationUser>()
            .WithOne(x => x.HouseholdMembership)
            .HasForeignKey<HouseholdMember>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_household_members_users_user_id");

        builder.HasIndex(x => x.UserId)
            .IsUnique()
            .HasDatabaseName("ux_household_members_user_id");

        builder.HasIndex(x => x.HouseholdId)
            .HasDatabaseName("ix_household_members_household_id");

        builder.HasIndex(x => x.HouseholdId)
            .IsUnique()
            .HasFilter("role = 'owner'")
            .HasDatabaseName("ux_household_members_household_owner");
    }
}
```

The partial unique index prevents two owners for the same household even if an application bug slips through.

## 26. Data Protection configuration

```csharp
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Averio.Infrastructure.Persistence.Configurations;

internal sealed class DataProtectionKeyConfiguration
    : IEntityTypeConfiguration<DataProtectionKey>
{
    public void Configure(EntityTypeBuilder<DataProtectionKey> builder)
    {
        builder.ToTable("data_protection_keys");
        builder.HasKey(x => x.Id).HasName("pk_data_protection_keys");
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.FriendlyName).HasColumnName("friendly_name");
        builder.Property(x => x.Xml).HasColumnName("xml");
    }
}
```

## 27. Unit of Work implementation

```csharp
using Averio.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Averio.Infrastructure.Persistence;

internal sealed class UnitOfWork(AverioDbContext dbContext) : IUnitOfWork
{
    public async Task<IApplicationTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken)
    {
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        return new ApplicationTransaction(transaction);
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private sealed class ApplicationTransaction(IDbContextTransaction transaction)
        : IApplicationTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken)
            => transaction.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken)
            => transaction.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => transaction.DisposeAsync();
    }
}
```

This makes Identity user creation + Household + HouseholdMember atomic because UserManager and the repository resolve the same scoped AverioDbContext.

## 28. Household repository

```csharp
using Averio.Application.Common.Abstractions;
using Averio.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace Averio.Infrastructure.Persistence.Repositories;

internal sealed class HouseholdRepository(AverioDbContext dbContext)
    : IHouseholdRepository
{
    public void Add(Household household)
    {
        dbContext.Households.Add(household);
    }

    public Task<HouseholdMember?> GetMembershipByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return dbContext.HouseholdMembers
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.UserId == userId,
                cancellationToken);
    }
}
```

## 29. Identity service

```csharp
using Averio.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Averio.Infrastructure.Identity;

internal sealed class IdentityService(
    UserManager<ApplicationUser> userManager,
    TimeProvider timeProvider)
    : IIdentityService
{
    public async Task<IdentityUserInfo?> FindByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        return user is null ? null : Map(user);
    }

    public async Task<IdentityUserInfo?> FindByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        return user is null ? null : Map(user);
    }

    public async Task<IdentityUserCreationResult> CreateUserAsync(
        string email,
        string password,
        string preferredCulture,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedEmail = email.Trim();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = normalizedEmail,
            Email = normalizedEmail,
            EmailConfirmed = false,
            LockoutEnabled = true,
            PreferredCulture = preferredCulture,
            CreatedAtUtc = timeProvider.GetUtcNow()
        };

        try
        {
            var result = await userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                return new IdentityUserCreationResult(true, user.Id, []);
            }

            var errorCodes = result.Errors
                .Select(MapIdentityError)
                .Distinct()
                .ToArray();

            return new IdentityUserCreationResult(false, null, errorCodes);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation
            })
        {
            return new IdentityUserCreationResult(
                false,
                null,
                [IdentityCreationErrorCodes.DuplicateEmail]);
        }
    }

    public async Task<string?> GenerateEmailVerificationTokenAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByIdAsync(userId.ToString());
        if (user is null) return null;
        return await userManager.GenerateEmailConfirmationTokenAsync(user);
    }

    public async Task<EmailConfirmationStatus> ConfirmEmailAsync(
        Guid userId,
        string token,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null)
            return EmailConfirmationStatus.UserNotFound;

        if (user.EmailConfirmed)
            return EmailConfirmationStatus.AlreadyConfirmed;

        var result = await userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded
            ? EmailConfirmationStatus.Succeeded
            : EmailConfirmationStatus.InvalidToken;
    }

    private static IdentityUserInfo Map(ApplicationUser user)
        => new(user.Id, user.Email!, user.EmailConfirmed, user.PreferredCulture);

    private static string MapIdentityError(IdentityError error)
    {
        return error.Code switch
        {
            "DuplicateEmail" => IdentityCreationErrorCodes.DuplicateEmail,
            "DuplicateUserName" => IdentityCreationErrorCodes.DuplicateEmail,
            _ => IdentityCreationErrorCodes.Rejected
        };
    }
}
```

## 30. Secure login implementation

```csharp
using Averio.Application.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Identity;

internal sealed class UserSessionService(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    TimeProvider timeProvider,
    ILogger<UserSessionService> logger)
    : IUserSessionService
{
    public async Task<SignInStatus> SignInAsync(
        string email,
        string password,
        bool rememberMe,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var user = await userManager.FindByEmailAsync(email.Trim());

        if (user is null)
            return SignInStatus.InvalidCredentials;

        if (await userManager.IsLockedOutAsync(user))
            return SignInStatus.LockedOut;

        // Check password FIRST so an attacker cannot learn that
        // an unverified email exists using the wrong password.
        var passwordValid = await userManager.CheckPasswordAsync(user, password);

        if (!passwordValid)
        {
            if (await userManager.GetLockoutEnabledAsync(user))
            {
                await userManager.AccessFailedAsync(user);

                if (await userManager.IsLockedOutAsync(user))
                    return SignInStatus.LockedOut;
            }

            return SignInStatus.InvalidCredentials;
        }

        await userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed)
            return SignInStatus.EmailNotConfirmed;

        user.LastLoginAtUtc = timeProvider.GetUtcNow();

        var updateResult = await userManager.UpdateAsync(user);

        if (!updateResult.Succeeded)
        {
            logger.LogWarning(
                "Could not update last login timestamp for user {UserId}",
                user.Id);
        }

        await signInManager.SignInAsync(user, isPersistent: rememberMe);

        return SignInStatus.Succeeded;
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await signInManager.SignOutAsync();
    }
}
```

## 31. Email configuration

```csharp
namespace Averio.Infrastructure.Email;

internal sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string Provider { get; set; } = "Development";
    public string FrontendBaseUrl { get; set; } = "http://localhost:5173";
    public string SenderEmail { get; set; } = string.Empty;
    public string SenderName { get; set; } = "Averio";
    public string BrevoApiKey { get; set; } = string.Empty;
}
```

## 32. Internal transactional email abstraction

```csharp
namespace Averio.Infrastructure.Email;

internal interface ITransactionalEmailSender
{
    Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken);
}
```

## 33. Development email sender

```csharp
using Microsoft.Extensions.Logging;

namespace Averio.Infrastructure.Email;

internal sealed class DevelopmentTransactionalEmailSender(
    ILogger<DevelopmentTransactionalEmailSender> logger)
    : ITransactionalEmailSender
{
    public Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            """
            DEVELOPMENT EMAIL

            To:
            {Recipient}

            Subject:
            {Subject}

            Body:
            {HtmlContent}
            """,
            recipient,
            subject,
            htmlContent);

        return Task.CompletedTask;
    }
}
```

## 34. Brevo sender

```csharp
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class BrevoTransactionalEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options)
    : ITransactionalEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task SendAsync(
        string recipient,
        string subject,
        string htmlContent,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.BrevoApiKey))
            throw new InvalidOperationException("Brevo API key is not configured.");

        if (string.IsNullOrWhiteSpace(_options.SenderEmail))
            throw new InvalidOperationException("Email sender address is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "v3/smtp/email");

        request.Headers.Add("api-key", _options.BrevoApiKey);

        request.Content = JsonContent.Create(new
        {
            sender = new
            {
                name = _options.SenderName,
                email = _options.SenderEmail
            },
            to = new[]
            {
                new
                {
                    email = recipient,
                    contactPixelTrackingConsent = false
                }
            },
            subject,
            htmlContent,
            tags = new[] { "email-verification" }
        });

        using var response = await httpClient.SendAsync(
            request,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Brevo returned HTTP {(int)response.StatusCode}.");
        }
    }
}
```

## 35. Email localization marker

```csharp
namespace Averio.Infrastructure;

public sealed class EmailMessages;
```

## 36. Verification email sender

```csharp
using System.Globalization;
using System.Text.Encodings.Web;
using Averio.Application.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Averio.Infrastructure.Email;

internal sealed class EmailVerificationSender(
    ITransactionalEmailSender transactionalEmailSender,
    IStringLocalizer<EmailMessages> localizer,
    IOptions<EmailOptions> options,
    ILogger<EmailVerificationSender> logger)
    : IEmailVerificationSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<bool> TrySendAsync(
        Guid userId,
        string email,
        string verificationToken,
        string culture,
        CancellationToken cancellationToken)
    {
        try
        {
            var cultureInfo = GetCulture(culture);
            var previousCulture = CultureInfo.CurrentCulture;
            var previousUiCulture = CultureInfo.CurrentUICulture;

            try
            {
                CultureInfo.CurrentCulture = cultureInfo;
                CultureInfo.CurrentUICulture = cultureInfo;

                var baseUrl = _options.FrontendBaseUrl.TrimEnd('/');

                var verificationUrl =
                    $"{baseUrl}/verify-email"
                    + $"?userId={userId:D}"
                    + $"&token={Uri.EscapeDataString(verificationToken)}";

                var encodedUrl = HtmlEncoder.Default.Encode(verificationUrl);

                var subject = localizer["Verification_Subject"].Value;
                var heading = HtmlEncoder.Default.Encode(localizer["Verification_Heading"].Value);
                var description = HtmlEncoder.Default.Encode(localizer["Verification_Description"].Value);
                var button = HtmlEncoder.Default.Encode(localizer["Verification_Button"].Value);
                var footer = HtmlEncoder.Default.Encode(localizer["Verification_Footer"].Value);

                var html = $$"""
                <!doctype html>
                <html>
                <body style="font-family:Arial,sans-serif;background:#f8fafc;padding:32px;">
                    <div style="max-width:560px;margin:0 auto;background:white;padding:32px;border-radius:16px;">
                        <h1 style="margin-top:0;">{{heading}}</h1>
                        <p>{{description}}</p>
                        <p style="margin:32px 0;">
                            <a href="{{encodedUrl}}"
                               style="display:inline-block;padding:12px 20px;background:#0f172a;color:white;text-decoration:none;border-radius:8px;">
                                {{button}}
                            </a>
                        </p>
                        <p style="font-size:13px;color:#64748b;">{{footer}}</p>
                    </div>
                </body>
                </html>
                """;

                await transactionalEmailSender.SendAsync(
                    email,
                    subject,
                    html,
                    cancellationToken);

                return true;
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Could not send verification email to user {UserId}",
                userId);

            return false;
        }
    }

    private static CultureInfo GetCulture(string culture)
    {
        return culture switch
        {
            "de-DE" => CultureInfo.GetCultureInfo("de-DE"),
            "hu-HU" => CultureInfo.GetCultureInfo("hu-HU"),
            _ => CultureInfo.GetCultureInfo("en-US")
        };
    }
}
```

## 37. Verification email resources

Create EmailMessages.en-US.resx, EmailMessages.de-DE.resx and EmailMessages.hu-HU.resx with localized subject, heading, description, button and footer strings.

English keys:

Verification_Subject = Verify your Averio email
Verification_Heading = Verify your email
Verification_Description = Confirm your email address to finish setting up your Averio account.
Verification_Button = Verify email
Verification_Footer = If you didn't create an Averio account, you can ignore this email.

German keys:

Verification_Subject = Bestätige deine Averio-E-Mail-Adresse
Verification_Heading = E-Mail-Adresse bestätigen
Verification_Description = Bestätige deine E-Mail-Adresse, um die Einrichtung deines Averio-Kontos abzuschließen.
Verification_Button = E-Mail bestätigen
Verification_Footer = Falls du kein Averio-Konto erstellt hast, kannst du diese E-Mail ignorieren.

Hungarian keys:

Verification_Subject = Erősítsd meg az Averio e-mail-címedet
Verification_Heading = E-mail-cím megerősítése
Verification_Description = Erősítsd meg az e-mail-címedet az Averio-fiókod beállításának befejezéséhez.
Verification_Button = E-mail megerősítése
Verification_Footer = Ha nem te hoztad létre ezt az Averio-fiókot, ezt az e-mailt figyelmen kívül hagyhatod.

## 38. Infrastructure DI

```csharp
using Averio.Application.Common.Abstractions;
using Averio.Application.Identity;
using Averio.Infrastructure.Email;
using Averio.Infrastructure.Identity;
using Averio.Infrastructure.Persistence;
using Averio.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Averio.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("AverioDatabase");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'AverioDatabase' is not configured.");
        }

        services.AddDbContextPool<AverioDbContext>(options =>
        {
            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null);
                });
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;

                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.Stores.MaxLengthForKeys = 128;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
            })
            .AddEntityFrameworkStores<AverioDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.Configure<DataProtectionTokenProviderOptions>(options =>
        {
            options.TokenLifespan = TimeSpan.FromHours(24);
        });

        services
            .AddDataProtection()
            .SetApplicationName("Averio")
            .PersistKeysToDbContext<AverioDbContext>();

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IHouseholdRepository, HouseholdRepository>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserSessionService, UserSessionService>();

        ConfigureEmail(services, configuration);

        return services;
    }

    private static void ConfigureEmail(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName));

        var provider = configuration[$"{EmailOptions.SectionName}:Provider"];

        if (string.Equals(provider, "Brevo", StringComparison.OrdinalIgnoreCase))
        {
            services.AddHttpClient<ITransactionalEmailSender, BrevoTransactionalEmailSender>(
                client =>
                {
                    client.BaseAddress = new Uri("https://api.brevo.com/");
                });
        }
        else
        {
            services.AddScoped<ITransactionalEmailSender, DevelopmentTransactionalEmailSender>();
        }

        services.AddScoped<IEmailVerificationSender, EmailVerificationSender>();
    }
}
```

Identity Schema Version2 is used deliberately so the first migration does not introduce passkeys before Averio actually implements them.

## 39. API contracts

```csharp
namespace Averio.Contracts.Authentication;

public sealed record RegisterUserRequest(string Email, string Password);

public sealed record RegisterUserResponse(
    Guid UserId,
    bool EmailVerificationRequired,
    bool VerificationEmailSent);

public sealed record LoginUserRequest(
    string Email,
    string Password,
    bool RememberMe);

public sealed record VerifyEmailRequest(Guid UserId, string Token);

public sealed record ResendEmailVerificationRequest(string Email);

public sealed record CurrentUserResponse(
    Guid UserId,
    string Email,
    bool EmailVerified,
    HouseholdMembershipResponse Household);

public sealed record HouseholdMembershipResponse(
    Guid HouseholdId,
    string Role);
```

Security contract:

```csharp
namespace Averio.Contracts.Security;

public sealed record CsrfTokenResponse(string Token);
```

## 40. API current-user implementation

```csharp
using System.Security.Claims;
using Averio.Application.Common.Abstractions;

namespace Averio.Api.Authentication;

public sealed class CurrentUser(IHttpContextAccessor httpContextAccessor)
    : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = httpContextAccessor
                .HttpContext?
                .User
                .FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(value, out var userId)
                ? userId
                : null;
        }
    }
}
```

## 41. Cookie authentication

.NET 10 improves cookie auth for known API endpoints by returning 401/403 rather than redirecting API clients to HTML login/access-denied pages. We still configure this explicitly for clarity.

## 42. CSRF protection

Cookie auth means the browser automatically sends the auth cookie. That's convenient but introduces CSRF risk.

Create apps/Averio.Api/Security/CsrfProtectionMiddleware.cs:

```csharp
using Microsoft.AspNetCore.Antiforgery;

namespace Averio.Api.Security;

public sealed class CsrfProtectionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery)
    {
        var requiresValidation =
            context.Request.Path.StartsWithSegments("/api/v1")
            && IsUnsafeMethod(context.Request.Method);

        if (requiresValidation)
        {
            await antiforgery.ValidateRequestAsync(context);
        }

        await next(context);
    }

    private static bool IsUnsafeMethod(string method)
    {
        return HttpMethods.IsPost(method)
            || HttpMethods.IsPut(method)
            || HttpMethods.IsPatch(method)
            || HttpMethods.IsDelete(method);
    }
}
```

## 43. CSRF controller

```csharp
using Averio.Contracts.Security;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
public sealed class SecurityController(IAntiforgery antiforgery)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("csrf")]
    [ProducesResponseType<CsrfTokenResponse>(StatusCodes.Status200OK)]
    public ActionResult<CsrfTokenResponse> GetCsrfToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new CsrfTokenResponse(tokens.RequestToken!));
    }
}
```

The browser gets an HttpOnly antiforgery cookie while JavaScript receives the request token and sends it in X-CSRF-TOKEN.

## 44. Auth controller

```csharp
using System.Globalization;
using Averio.Application.Identity;
using Averio.Application.Identity.GetCurrentUser;
using Averio.Application.Identity.Login;
using Averio.Application.Identity.RegisterUser;
using Averio.Application.Identity.ResendVerification;
using Averio.Application.Identity.VerifyEmail;
using Averio.Contracts.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Averio.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    RegisterUserHandler registerUserHandler,
    LoginUserHandler loginUserHandler,
    VerifyEmailHandler verifyEmailHandler,
    ResendEmailVerificationHandler resendHandler,
    GetCurrentUserHandler currentUserHandler,
    IUserSessionService userSessionService)
    : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<RegisterUserResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<RegisterUserResponse>> Register(
        RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await registerUserHandler.HandleAsync(
            new RegisterUserCommand(
                request.Email,
                request.Password,
                CultureInfo.CurrentUICulture.Name),
            cancellationToken);

        var response = new RegisterUserResponse(
            result.UserId,
            EmailVerificationRequired: true,
            result.VerificationEmailSent);

        return Created("/api/v1/auth/me", response);
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        LoginUserRequest request,
        CancellationToken cancellationToken)
    {
        await loginUserHandler.HandleAsync(
            new LoginUserCommand(
                request.Email,
                request.Password,
                request.RememberMe),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await userSessionService.SignOutAsync(cancellationToken);
        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification")]
    public async Task<IActionResult> VerifyEmail(
        VerifyEmailRequest request,
        CancellationToken cancellationToken)
    {
        await verifyEmailHandler.HandleAsync(
            new VerifyEmailCommand(request.UserId, request.Token),
            cancellationToken);

        return NoContent();
    }

    [AllowAnonymous]
    [HttpPost("email-verification/resend")]
    public async Task<IActionResult> ResendVerification(
        ResendEmailVerificationRequest request,
        CancellationToken cancellationToken)
    {
        await resendHandler.HandleAsync(
            new ResendEmailVerificationCommand(request.Email),
            cancellationToken);

        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var result = await currentUserHandler.HandleAsync(cancellationToken);

        return Ok(new CurrentUserResponse(
            result.UserId,
            result.Email,
            result.EmailVerified,
            new HouseholdMembershipResponse(
                result.HouseholdId,
                result.HouseholdRole.ToString().ToLowerInvariant())));
    }
}
```

## 45. Update Program.cs

```csharp
using System.Diagnostics;
using System.Globalization;
using Averio.Api.Authentication;
using Averio.Api.ErrorHandling;
using Averio.Api.Security;
using Averio.Application;
using Averio.Application.Common.Abstractions;
using Averio.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id
            ?? context.HttpContext.TraceIdentifier;
    };
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddLocalization(options =>
{
    options.ResourcesPath = "Resources";
});

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("en-US"),
        new CultureInfo("de-DE"),
        new CultureInfo("hu-HU")
    };

    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.ApplyCurrentCultureToResponseHeaders = true;

    options.RequestCultureProviders =
    [
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = IdentityConstants.ApplicationScheme;
        options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
    })
    .AddIdentityCookies();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "averio.auth"
        : "__Host-averio.auth";

    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Lax;

    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;

    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };

    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = builder.Environment.IsDevelopment()
        ? "averio.csrf"
        : "__Host-averio.csrf";

    options.Cookie.HttpOnly = true;
    options.Cookie.Path = "/";
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
});

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .GetChildren()
    .Select(x => x.Value)
    .Where(x => !string.IsNullOrWhiteSpace(x))
    .Cast<string>()
    .ToArray();

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseRequestLocalization();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<CsrfProtectionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.MapScalarApiReference(options =>
    {
        options
            .WithTitle("Averio API")
            .DisableAgent();
    });
}

app.MapControllers();
app.Run();

public partial class Program;
```

## 46. Update exception handling

Use GlobalExceptionHandler to map:

- FluentValidation ValidationException → 400 validation_failed
- AntiforgeryValidationException → 400 invalid_csrf_token
- AuthenticationFailedException → 401 invalid_credentials
- EmailNotVerifiedException → 403 email_not_verified
- AccountLockedException → 423 account_locked
- InvalidEmailVerificationTokenException → 400 invalid_email_verification_token
- ConflictException → 409
- HouseholdRequiredException → 409 household_required
- BusinessRuleException → 422
- unknown exceptions → 500 unexpected_error

Always include a stable machine-readable code and traceId, while title/detail remain localized.

## 47. Add API localization messages

Add English/German/Hungarian translations for:

Title_Validation
Title_Unauthorized
Title_Forbidden
Title_Locked
Title_InvalidRequest
Title_HouseholdRequired
Error_InvalidCredentials
Error_EmailNotVerified
Error_AccountLocked
Error_InvalidEmailVerificationToken
Error_EmailAlreadyRegistered
Error_HouseholdRequired
Error_IdentityCreationFailed
Error_InvalidCsrf
Validation_EmailRequired
Validation_EmailInvalid
Validation_EmailTooLong
Validation_PasswordRequired
Validation_PasswordTooShort
Validation_PasswordTooLong
Validation_UserIdRequired
Validation_TokenRequired

The stable error code must never change with language.

## 48. Update appsettings.json

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Cors": {
    "AllowedOrigins": [
      "http://localhost:5173"
    ]
  },
  "ConnectionStrings": {
    "AverioDatabase": ""
  },
  "Email": {
    "Provider": "Development",
    "FrontendBaseUrl": "http://localhost:5173",
    "SenderEmail": "noreply@averioapp.com",
    "SenderName": "Averio",
    "BrevoApiKey": ""
  }
}
```

Production:

```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://averioapp.com",
      "https://www.averioapp.com"
    ]
  },
  "Email": {
    "Provider": "Brevo",
    "FrontendBaseUrl": "https://averioapp.com",
    "SenderEmail": "noreply@averioapp.com",
    "SenderName": "Averio"
  }
}
```

Production secret:

Email__BrevoApiKey

Never commit the real Brevo API key.

## 49. The exact database tables

Once migrated, Averio will have:

users — credentials and identity
user_claims — future claims
user_logins — Google/Facebook/etc.
user_tokens — Identity token store
households — Averio household
household_members — User ↔ household relationship
data_protection_keys — Cookie/token crypto keys
__EFMigrationsHistory — EF migration history

There are no ASP.NET Identity role tables and no passkey table in the first migration.

## 50. Important PostgreSQL constraints

```sql
CREATE UNIQUE INDEX ux_users_normalized_email
    ON users (normalized_email);

CREATE UNIQUE INDEX ux_users_normalized_user_name
    ON users (normalized_user_name);

CREATE UNIQUE INDEX ux_household_members_user_id
    ON household_members (user_id);

CREATE INDEX ix_household_members_household_id
    ON household_members (household_id);

CREATE UNIQUE INDEX ux_household_members_household_owner
    ON household_members (household_id)
    WHERE role = 'owner';

ALTER TABLE household_members
    ADD CONSTRAINT ck_household_members_role
    CHECK (role IN ('owner', 'member'));
```

These encode the fundamental invariants directly into PostgreSQL.

## 51. Create the first migration

```powershell
dotnet restore
dotnet build
dotnet tool restore
```

Then:

```powershell
dotnet ef migrations add InitialIdentityHousehold `
    --project src/Averio.Infrastructure/Averio.Infrastructure.csproj `
    --startup-project apps/Averio.Api/Averio.Api.csproj `
    --output-dir Persistence/Migrations
```

The startup project matters because Identity options affecting the EF model, including schema version, must be present during migration generation.

Expected files:

```text
Averio.Infrastructure/
└── Persistence/
    └── Migrations/
        ├── <timestamp>_InitialIdentityHousehold.cs
        ├── <timestamp>_InitialIdentityHousehold.Designer.cs
        └── AverioDbContextModelSnapshot.cs
```

Do not hand-write this migration. Let EF generate it from the model.

## 52. Inspect the migration before applying it

```powershell
dotnet ef migrations script `
    --project src/Averio.Infrastructure/Averio.Infrastructure.csproj `
    --startup-project apps/Averio.Api/Averio.Api.csproj
```

Verify that you see:

users
user_claims
user_logins
user_tokens
households
household_members
data_protection_keys

and do NOT see:

AspNetRoles
AspNetUserRoles
AspNetRoleClaims
AspNetUserPasskeys

Also verify:

normalized_email UNIQUE
user_id UNIQUE
owner partial UNIQUE
role CHECK

If those aren't there, stop before running the migration.

## 53. Apply it to Neon

```powershell
dotnet ef database update `
    --project src/Averio.Infrastructure/Averio.Infrastructure.csproj `
    --startup-project apps/Averio.Api/Averio.Api.csproj
```

Now Neon contains the actual first Averio v2 schema.

## 54. What happens during registration

```text
POST /api/v1/auth/register

BEGIN TRANSACTION

Create users row
        │
        ▼
users
id = A

Create Household
        │
        ▼
households
id = B

Create owner membership
        │
        ▼
household_members
user_id      = A
household_id = B
role         = owner

COMMIT
```

Only after commit:

```text
Generate verification token
        ↓
Send email
```

## 55. Why email tokens aren't another database table

With the standard ASP.NET Identity Data Protection token provider, the email verification token isn't just a database row with token/expiry. Identity generates a protected token based on the user and security state.

That's why data_protection_keys are crucial.

## 56. Add CSRF support to the frontend

src/shared/api/csrf.ts

```typescript
import { API_BASE_URL } from './apiConfiguration';

interface CsrfTokenResponse {
    token: string;
}

let cachedToken: string | null = null;
let tokenPromise: Promise<string> | null = null;

export async function getCsrfToken(): Promise<string> {
    if (cachedToken) {
        return cachedToken;
    }

    if (tokenPromise) {
        return tokenPromise;
    }

    tokenPromise = loadCsrfToken();

    try {
        cachedToken = await tokenPromise;
        return cachedToken;
    }
    finally {
        tokenPromise = null;
    }
}

export function clearCsrfToken(): void {
    cachedToken = null;
    tokenPromise = null;
}

async function loadCsrfToken(): Promise<string> {
    const response = await fetch(
        `${API_BASE_URL}/api/v1/security/csrf`,
        {
            method: 'GET',
            credentials: 'include',
            headers: {
                Accept: 'application/json',
            },
        },
    );

    if (!response.ok) {
        throw new Error('Could not retrieve CSRF token.');
    }

    const data = await response.json() as CsrfTokenResponse;
    return data.token;
}
```

## 57. Update apiFetch

```typescript
import {
    API_BASE_URL,
    getApiCulture,
} from './apiConfiguration';

import {
    ApiError,
    type ProblemDetails,
} from './ApiError';

import {
    getCsrfToken,
} from './csrf';

const unsafeMethods = new Set([
    'POST',
    'PUT',
    'PATCH',
    'DELETE',
]);

export async function apiFetch<T>(
    path: string,
    init?: RequestInit,
): Promise<T> {
    const method = (init?.method ?? 'GET').toUpperCase();
    const headers = new Headers(init?.headers);

    headers.set('Accept', 'application/json');
    headers.set('Accept-Language', getApiCulture());

    if (
        init?.body
        && !headers.has('Content-Type')
        && !(init.body instanceof FormData)
    ) {
        headers.set('Content-Type', 'application/json');
    }

    if (unsafeMethods.has(method)) {
        const csrfToken = await getCsrfToken();
        headers.set('X-CSRF-TOKEN', csrfToken);
    }

    const response = await fetch(
        `${API_BASE_URL}${path}`,
        {
            ...init,
            method,
            headers,
            credentials: 'include',
        },
    );

    if (!response.ok) {
        let problem: ProblemDetails | undefined;

        try {
            problem = await response.json() as ProblemDetails;
        }
        catch {
            // No ProblemDetails payload.
        }

        throw new ApiError(
            problem?.detail
                ?? problem?.title
                ?? `Request failed with status ${response.status}.`,
            response.status,
            problem?.code,
            problem?.traceId,
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return await response.json() as T;
}
```

## 58. Auth query keys

```typescript
export const authQueryKeys = {
    all: [
        'authentication',
    ] as const,

    currentUser: () => [
        ...authQueryKeys.all,
        'current-user',
    ] as const,
};
```

Do not scatter raw auth query keys across components.

## 59. Auth API

After the backend is running, regenerate the API client:

```powershell
npm run api:generate
```

Then create src/features/authentication/api/authApi.ts:

```typescript
import type {
    CurrentUserResponse,
    LoginUserRequest,
    RegisterUserRequest,
    RegisterUserResponse,
    ResendEmailVerificationRequest,
    VerifyEmailRequest,
} from '../../../shared/api/generated';

import { apiFetch } from '../../../shared/api/apiFetch';
import { clearCsrfToken } from '../../../shared/api/csrf';

export async function registerUser(
    request: RegisterUserRequest,
): Promise<RegisterUserResponse> {
    return await apiFetch<RegisterUserResponse>(
        '/api/v1/auth/register',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function loginUser(
    request: LoginUserRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/login',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );

    clearCsrfToken();
}

export async function logoutUser(): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/logout',
        {
            method: 'POST',
        },
    );

    clearCsrfToken();
}

export async function verifyEmail(
    request: VerifyEmailRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/email-verification',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function resendEmailVerification(
    request: ResendEmailVerificationRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/email-verification/resend',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function getCurrentUser(): Promise<CurrentUserResponse> {
    return await apiFetch<CurrentUserResponse>(
        '/api/v1/auth/me',
    );
}
```

## 60. Current-user TanStack query

```typescript
import { queryOptions } from '@tanstack/react-query';
import { getCurrentUser } from './authApi';
import { authQueryKeys } from './authQueryKeys';

export const authQueries = {
    currentUser: () =>
        queryOptions({
            queryKey: authQueryKeys.currentUser(),
            queryFn: getCurrentUser,
            retry: false,
            staleTime: 60_000,
        }),
};
```

## 61. What cookie gets stored

After login the browser gets a cookie conceptually like:

averio.auth=<encrypted value>

Development:

HttpOnly
SameSite=Lax
Path=/

Production:

__Host-averio.auth
HttpOnly
Secure
SameSite=Lax
Path=/

There is deliberately no Domain=.averioapp.com. The cookie only needs to belong to api.averioapp.com.

## 62. Testing the complete registration flow

Start the API:

```powershell
dotnet build

dotnet run `
    --project apps/Averio.Api
```

Create an HTTP session:

```powershell
$session = New-Object `
    Microsoft.PowerShell.Commands.WebRequestSession
```

Get CSRF:

```powershell
$csrf = (
    Invoke-RestMethod `
        -Uri "http://localhost:5180/api/v1/security/csrf" `
        -WebSession $session
).token
```

Register:

```powershell
$body = @{
    email = "peter@example.com"
    password = "correct horse battery staple"
} | ConvertTo-Json
```

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5180/api/v1/auth/register" `
    -WebSession $session `
    -Headers @{
        "X-CSRF-TOKEN" = $csrf
        "Accept-Language" = "en-US"
    } `
    -ContentType "application/json" `
    -Body $body
```

Expected response:

```json
{
  "userId": "...",
  "emailVerificationRequired": true,
  "verificationEmailSent": true
}
```

## 63. Check the API console

Because Provider is Development, the API console shows the generated verification email and link:

http://localhost:5173/verify-email?userId=...&token=...

This validates the token-generation/email-composition flow without Brevo.

## 64. Verify what was inserted in Neon

```sql
SELECT
    id,
    email,
    email_confirmed,
    created_at_utc
FROM users;
```

```sql
SELECT *
FROM households;
```

```sql
SELECT
    hm.user_id,
    hm.household_id,
    hm.role,
    hm.joined_at_utc
FROM household_members hm;
```

You should see:

```text
users
────────────────────
Peter
email_confirmed=false
```

```text
households
────────────────────
Household X
```

```text
household_members
────────────────────
Peter
Household X
owner
```

## 65. Verify the email through the API

Decode the token from the URL if needed:

```powershell
$token = [System.Uri]::UnescapeDataString(
    "TOKEN_FROM_URL"
)
```

Then:

```powershell
$verifyBody = @{
    userId = "YOUR_USER_ID"
    token = $token
} | ConvertTo-Json
```

```powershell
Invoke-RestMethod `
    -Method Post `
    -Uri "http://localhost:5180/api/v1/auth/email-verification" `
    -WebSession $session `
    -Headers @{
        "X-CSRF-TOKEN" = $csrf
    } `
    -ContentType "application/json" `
    -Body $verifyBody
```

Expected: 204 No Content.

Then:

```sql
SELECT
    email,
    email_confirmed
FROM users;
```

should show true.

## 66. Login

```powershell
$loginBody = @{
    email = "peter@example.com"
    password = "correct horse battery staple"
    rememberMe = $true
} | ConvertTo-Json
```

```powershell
Invoke-WebRequest `
    -Method Post `
    -Uri "http://localhost:5180/api/v1/auth/login" `
    -WebSession $session `
    -Headers @{
        "X-CSRF-TOKEN" = $csrf
    } `
    -ContentType "application/json" `
    -Body $loginBody
```

Expected: 204. The session now contains the auth cookie.

## 67. Refresh CSRF after login

```powershell
$csrf = (
    Invoke-RestMethod `
        -Uri "http://localhost:5180/api/v1/security/csrf" `
        -WebSession $session
).token
```

## 68. Call /me

```powershell
Invoke-RestMethod `
    -Uri "http://localhost:5180/api/v1/auth/me" `
    -WebSession $session
```

Expected:

```json
{
  "userId": "8ea...",
  "email": "peter@example.com",
  "emailVerified": true,
  "household": {
    "householdId": "a83...",
    "role": "owner"
  }
}
```

The frontend gets auth bootstrap state in one request: who the user is, what household they belong to, and what their household role is.

## 69. Login before verification

A correct password on an unverified account returns 403 with code email_not_verified. A wrong password still returns invalid_credentials, preventing email-status enumeration.

Localized text can change, but the stable error code must not.

## 70. Bad password lockout

Configuration:

```text
5 failed attempts
        ↓
15 minute lockout
```

Identity stores this in users.access_failed_count, users.lockout_end and users.lockout_enabled. No custom login_attempts table is required for normal lockout.

## 71. Enable real Brevo emails

```powershell
dotnet user-secrets set `
    --project apps/Averio.Api `
    "Email:Provider" `
    "Brevo"
```

```powershell
dotnet user-secrets set `
    --project apps/Averio.Api `
    "Email:BrevoApiKey" `
    "YOUR_BREVO_API_KEY"
```

Make sure noreply@averioapp.com is a verified Brevo sender/domain.

## 72. Regenerate the TypeScript API

```powershell
cd apps/averio-web
npm run api:generate
npm run build
```

This verifies that .NET contracts → OpenAPI → OpenAPI Generator → TypeScript → Vite build all agree.

## 73. The complete registration architecture

```text
                        React
                          │
                   POST register
                          │
                    X-CSRF-TOKEN
                          │
                          ▼
                     AuthController
                          │
                          ▼
                  RegisterUserHandler
                          │
               ┌──────────┴──────────┐
               │                     │
               ▼                     ▼
        ASP.NET Identity         Household
               │                     │
               ▼                     ▼
             users              households
                                     │
                                     ▼
                              household_members
                                     │
                         ┌───────────┴──────────┐
                         │                      │
                      user_id               role
                         │                   owner
                         ▼
                       users

                    ONE TRANSACTION
                          │
                          ▼
                        COMMIT
                          │
                          ▼
              Generate confirmation token
                          │
                          ▼
                      Brevo
                          │
                          ▼
                    User inbox
```

## 74. Why this household model is useful

Suppose registration produces:

```text
Peter
  │
  ▼
Household A
```

and another registration produces:

```text
Partner
  │
  ▼
Household B
```

When invitations arrive later, don't allow Partner to be simultaneously attached to two households. Instead implement an explicit HouseholdMerge operation:

```text
Household A
+
Household B
      │
      ▼
transaction
      │
      ├── choose surviving household
      ├── move accounts
      ├── move categories
      ├── move goals
      ├── move transactions if required
      ├── update Partner membership
      └── retire old household
```

The unique household_members.user_id constraint keeps protecting the invariant.

## 75. Things I would NOT add yet

- ❌ JWT access tokens
- ❌ refresh tokens
- ❌ ASP.NET Identity Roles
- ❌ household permissions
- ❌ invitations
- ❌ Google login
- ❌ Facebook login
- ❌ password reset
- ❌ 2FA
- ❌ passkeys
- ❌ Worker
- ❌ outbox
- ❌ Redis sessions

The database is deliberately ready for some of these through user_logins, user_tokens and data_protection_keys, but we don't have to implement them yet.

## 76. What is now established permanently

User identifier
    Guid

Identity
    ASP.NET Core Identity

Global roles
    none

Household ownership
    HouseholdMember.Role

User household cardinality
    exactly one

Registration
    User + Household + Owner membership atomic

Authentication
    secure HttpOnly cookie

Cookie persistence
    Data Protection keys in PostgreSQL

CSRF
    antiforgery cookie + X-CSRF-TOKEN

Verification
    Identity Data Protection token

Verification lifetime
    24 hours

Lockout
    5 failures / 15 minutes

Email
    provider abstraction

Development email
    logger

Production email
    Brevo

API language
    Accept-Language

Frontend API access
    TanStack Query → feature API layer

Machine-readable errors
    stable code

## Where I would go next

I would not implement Accounts next yet.

There is one more layer that should come before financial data:

```text
Identity + Household foundation
              ↓
Authentication UI
              ↓
Onboarding state
              ↓
Household profile
              ↓
Account model
```

Specifically, the next implementation should contain:

/register
/login
/verify-email
/resend-verification

AuthProvider / authentication bootstrap
Protected routes

first-login detection
onboarding status

Household name
preferred currency
country
user display name

GET /households/{id}
PATCH /households/{id}

onboarding completion

Only after that would I design Account, because the new Account model has to get shared/private ownership correct from the beginning.

Commercially, authentication itself should never be a paid feature, but the User → HouseholdMember → Household boundary established here is exactly what can later support differentiated collaborative features such as partner invitations, richer permissions, household analytics, family planning and advanced shared-finance controls without rewriting authentication or the financial core.

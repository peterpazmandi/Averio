namespace Averio.Contracts.Authentication;

public sealed record RegisterUserRequest(string Email, string Password);
public sealed record RegisterUserResponse(Guid UserId, bool EmailVerificationRequired, bool VerificationEmailSent);
public sealed record LoginUserRequest(string Email, string Password, bool RememberMe);
public sealed record VerifyEmailRequest(Guid UserId, string Token);
public sealed record ResendEmailVerificationRequest(string Email);
public sealed record RequestPasswordResetRequest(string Email);
public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);
public sealed record CurrentUserResponse(Guid UserId, string Email, string? DisplayName, bool EmailVerified, bool OnboardingRequired, HouseholdMembershipResponse Household);
public sealed record HouseholdMembershipResponse(Guid HouseholdId, string Role);
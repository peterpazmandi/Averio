namespace Averio.Contracts.Profile;

public sealed record PasskeyCreationOptionsRequest(string Password);
public sealed record RegisterPasskeyRequest(string CredentialJson, string Name);
public sealed record RemovePasskeyRequest(string Password);
public sealed record PasskeyResponse(string CredentialId, string Name, DateTimeOffset CreatedAt);
public sealed record PasskeySignInRequest(string CredentialJson, bool RememberMe);

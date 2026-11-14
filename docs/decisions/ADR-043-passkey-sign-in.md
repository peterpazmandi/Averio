# ADR-043: Passkey sign-in

**Status:** Accepted

**Date:** 2026-11-14

## Context

Averio users want to sign in using a fingerprint or other device verification. The current profile uses ASP.NET Core Identity, password sign-in and TOTP MFA, with a React app on a separate origin from the API.

## Decision

- Use ASP.NET Core Identity's WebAuthn passkey support with its Version 3 database schema. Store public credentials in `user_passkeys`; never collect biometric data.
- Require an authenticated session and the current password before issuing passkey creation options. Bind registration to the authenticated user's ID, limit each user to ten passkeys, and require the password to remove a passkey.
- Configure the RP ID to the frontend hostname and validate the signed WebAuthn origin against the exact CORS origin allowlist and the request's Origin header. Cross-origin frames are rejected. Production and integration therefore use distinct RP IDs and credentials.
- A verified passkey with device user verification completes sign-in without a separate TOTP code. Password sign-in still requires TOTP when MFA is enabled. Passkeys remain an optional sign-in method; password and existing recovery flows continue.
- For .NET 10, perform assertion validation directly and enforce email confirmation and lockout before issuing the cookie. Persist the updated signature counter and last login time. Restrict anonymous attempts per IP.

## Consequences

- Apply the migration before the API deployment. The same Identity data-protection key ring must be shared across instances for the short-lived WebAuthn challenge state.
- Passkeys require a secure browser context (HTTPS outside localhost). Some devices use a PIN or face recognition instead of a fingerprint; Averio cannot choose a biometric modality.
- If the frontend domain changes, previously enrolled passkeys may no longer work. Production and integration credentials must be enrolled separately.

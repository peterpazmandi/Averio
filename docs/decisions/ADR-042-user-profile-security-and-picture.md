# ADR-042: User profile security and profile pictures

**Status:** Accepted

**Date:** 2026-11-14

## Context

Averio needs a personal profile for the authenticated user, including profile picture management and multi-factor authentication. The app uses ASP.NET Core Identity with cookie authentication and PostgreSQL. User-facing profile information must remain private to the signed-in user unless a later household feature explicitly exposes it.

## Decision

- Store a normalized JPEG profile picture on the user's Identity record. Accept JPEG, PNG, and WebP uploads up to 5 MB; crop to a centered 256 × 256 image, re-encode as JPEG, and cap the stored image at 256 KB. Re-encoding removes source metadata. Pictures are served only from an authenticated endpoint.
- Use the existing ASP.NET Core Identity authenticator token provider for time-based one-time password (TOTP) MFA. Require the current password to begin enrollment, verify a code before enabling MFA, and issue ten one-time recovery codes. Require the password and a current authenticator code to disable MFA or replace recovery codes.
- When MFA is enabled, password sign-in creates Identity's short-lived two-factor challenge. Only a valid authenticator or recovery code completes sign-in and issues the normal application cookie. Keep the ordinary no-MFA sign-in response as `204 No Content`; return `202 Accepted` with a challenge indicator only when a second factor is required.
- Store and update display name and supported preferred culture (`en-US`, `de-DE`, `hu-HU`) on the user record. Email remains read-only until Averio has an email-change confirmation flow.

## Consequences

- Profile pictures need no external object-storage bucket and are covered by the existing user data lifecycle. Their strict size cap keeps the added database storage bounded.
- MFA enrollment and sign-in depend on the Identity shared data-protection key ring already used for authentication cookies and tokens. Deployments must continue sharing that key ring between API instances.
- The first enrollment UI supports the authenticator's manual secret entry and URI handoff. A QR-code renderer and phishing-resistant passkeys are follow-up work.
- Current session inventory/revocation and account export/deletion remain separate work; this change does not claim to provide them.

## Security notes

- Never log authenticator secrets, recovery codes, or uploaded image contents.
- Profile pictures are not added to household member responses in this change; authenticated users can retrieve only their own picture.
- Recovery codes are shown only immediately after issuance or regeneration. A regeneration invalidates all earlier codes.

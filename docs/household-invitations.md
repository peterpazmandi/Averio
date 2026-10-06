# Household invitations and acceptance

Household owners can invite a partner from Household settings, inspect delivery
status, resend a pending/expired link, or revoke it. Members cannot access this
email directory. Accepting grants the Member role and the existing shared-finance
permissions; it does not grant access to private accounts or personal activities.

## Joining

1. Open the email link. Opening a link never changes membership.
2. Sign in with the invited email, or register and verify that address first.
3. Review the household name and the replacement explanation, explicitly confirm,
   and choose Accept and join.

Invitation context stays in the current browser tab's session storage across
registration, verification and login, with a seven-day local expiry. The login
route resumes the invitation before solo onboarding. If verification is opened
in a separate browser/tab without that context, reopen the original invitation
email link after verification. Tokens are removed from the URL fragment after
loading, and cleared on successful acceptance or Not now. They are never query
parameters, query-cache keys, or server access-log paths. POST bodies containing
tokens must remain excluded from any future request-body logging.

Only verified users with the matching normalized email can preview or accept.
Wrong-email, revoked, expired and rotated links receive a generic invalid-invitation
error without household details. Switching accounts is available on the review page.

The source household must contain exactly its owner and no persisted dependents:
accounts (including archived), transactions (including voided), transfers,
categories, labels, activities, assignments, pins or invitations/history. Global
seeded catalogs and the user's profile do not block joining. Shared households
and households with data are preserved and cannot join. There is no financial
history migration or deletion workflow. Only a verified-empty disposable source
household and its membership are removed. Acceptance records the source household
identifier, accepting user and timestamp for audit.

## Atomicity and dependent writes

Acceptance locks the user row, both household rows in ascending ID order, and
the source membership. It rechecks the token, current membership, inviter's owner
role, target completion and source dependencies under the transaction. Revocation
and resend lock the same target household, so only one lifecycle action can win.
Repeated acceptance by the same user returns success without another membership.
The unique user-membership constraint remains in force.

PostgreSQL foreign-key KEY SHARE locks participate in the joining protocol for
every dependent insert: they conflict with the acceptance FOR UPDATE parent locks.
An insert that wins is visible to subsequent READ COMMITTED eligibility queries;
an insert that loses cannot reference the deleted source. Both household and member
parents are locked, including member-only dependencies such as pins. No eligibility
decision relies on cascade deletion. The check enumerates mapped FKs to these
parents and checks all rows without visibility/archive filters. Future dependencies
must have enforced, immediate FKs and be covered by a joining regression. Unmapped
tables, deferred constraints or external stores require extending this protocol
before shipping.

After acceptance the frontend cancels all queries, clears query and mutation
caches, refreshes identity, and opens the target household. No solo first-account
setup is required for a joined household. An optional display name can remain
unset; the existing member-directory fallback applies.

## Email and lifecycle

- 256-bit random tokens; SHA-256 hashes only in the database; seven-day validity.
- Pending, Accepted and Revoked are persisted; Expired is derived from expiry.
- One Pending row per household/normalized email, including expired rows: resend
  it or revoke it before creating another. Resend rotates the token and version.
- One-minute resend cooldown, ten sends per invitation, twenty new invitations
  per household per rolling day; the existing email IP rate limiter also applies.
- State is committed before synchronous email delivery. Sent means the provider
  accepted the message, not that the recipient read or received it. Failed and
  Unknown remain visible and can be retried by resending. A crash or cancellation
  between persistence and delivery leaves Unknown. There is no durable email queue
  or automatic retry in this slice. A delivery update cannot overwrite the status
  of a newer resend/revocation because it checks the lifecycle version.
- EN/DE/HU emails use the requesting owner's current API culture.
- Configure `Email:Provider=Brevo`, `Email:BrevoApiKey`, `Email:SenderEmail`,
  `Email:SenderName`, and the deployed HTTPS `Email:FrontendBaseUrl`. Secrets belong
  in environment configuration. The development logger sender is intentionally
  not used for invitations, so raw invitation links never enter server logs;
  without Brevo, delivery is recorded as Failed. Tests use a capturing fake sender.

## API and deployment

- GET/POST `/api/v1/households/{householdId}/invitations`: owner list/create.
- POST `.../invitations/{invitationId}/resends` and `/revocations`: owner command
  with `expectedVersion`.
- POST `/api/v1/invitations/preview`: verified email-matched, non-consuming preview.
- POST `/api/v1/invitations/acceptances`: explicit, atomic join; body `{ token }`.

All routes require authentication; the existing cookie CSRF middleware protects
POST requests. Apply `20261006073953_AddHouseholdInvitations` explicitly before API
deployment, then deploy the regenerated frontend client. The migration adds the
invitation table, indexes, lifecycle checks and restrictive FKs; existing financial
data is unchanged. Do not roll the schema back after accepting invitations unless
loss of invitation audit history has been addressed.

## Verification and boundaries

The PostgreSQL suite exercises permissions, failed delivery, duplicates, rotation,
cooldowns, wrong/unverified email, preview without mutation, concurrent acceptance,
private-account visibility, revoked/expired links, and dependency preservation.
Frontend regression coverage includes tab context expiry and the existing settings
reload race. Browser verification should cover owner send/revoke, anonymous link ->
login -> review -> confirmation -> target household, and responsive EN/DE/HU states.

Member removal, leaving, ownership transfer, automatic history merging and
multi-household switching remain deferred under ADR-023. A wider collaboration
release still needs a supported departure/recovery policy.

# Household invitations and acceptance

Household owners can invite a partner from Household settings, inspect delivery
status, resend a pending/expired link, or revoke it. Members cannot access this
email directory. Accepting grants the Member role and the existing shared-finance
permissions; it does not grant access to private accounts or personal activities.

## Joining and account cutover

1. Open the invitation and sign in, or register and verify the invited email.
2. Review the destination household and departure plan. A new user sees this flow
   before any household-create or first-account form.
3. Choose personal/private, personal/shared, or household-owned handling for every
   active personal account held solely by you. Household-owned and jointly held
   accounts stay behind; archived accounts are not copied.
4. If you own a household with remaining members, select its next owner.
5. Explicitly confirm and accept. A changed account, balance or household requires
   a fresh review. You can also decline and resume your existing or solo household.

Acceptance creates new account identities with current balances as opening balances
on the acceptance date. No source transactions, transfers, custom categories,
activities or other financial context enter the destination household. Private
copies are excluded from household reports; shared personal and household-owned
copies participate in reports. Currency is preserved without conversion.

The original accounts and ledger remain in the source household. Copied source
accounts are permanently closed and cannot be restored or written to. Their
historical reporting participation is retained, so source cash flow does not change.
Remaining members see only what their existing permissions permit and see former
holder attribution. Household-owned and joint accounts stay active under existing
permissions. If the last member leaves, the household is archived, never deleted.

The departing user can open their previous-household history in Household settings.
This is an immutable snapshot of data visible at departure, including private data
only they could see and redacted transfer legs. Later source writes or edits cannot
appear. History is read-only, belongs exclusively to that former user, and never
contributes to destination totals. Date-filtered cash flow retains original
currencies and excludes private transactions, voided entries, transfers and opening
balances. An empty, unconfigured registration placeholder does not create a history
card.

Invitation context remains in tab session storage across registration, verification
and login for up to seven days. Verified recipients also discover their pending
invitations server-side, so another verification tab resumes invitation onboarding.
Tokens are removed from URL fragments after loading and cleared after acceptance
or deferral. They are never query parameters or query-cache keys. POST token bodies
must remain excluded from request-body logging. No recipient or household details
are disclosed until authenticated, verified email matching succeeds.

## Atomicity and authorization

Acceptance locks the identity user and source/destination households in ascending
ID order. Under that transaction it rechecks lifecycle, inviter authority, active
membership, target setup, source version, eligible account IDs, exact account
version tokens, balances, handling choices and successor. Snapshot, departure,
owner transfer, destination membership, copies and acceptance commit together.
Retries of an already accepted invitation by that member are idempotent.

Financial mutation handlers lock the household before reading or authorizing and
recheck active membership after acquiring the lock. This serializes ledger and
metadata writes with cutover. Active memberships and household owners use filtered
unique indexes; old memberships remain as foreign-key targets for authorship and
holders. Departed users have no live authorization in the source household.

After acceptance the frontend cancels queries, clears query/mutation caches,
refreshes identity and opens the destination. Joined users do not run household
creation or first-account setup. An optional display name is collected separately.

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
- Branded HTML emails use the official Averio logo, emerald accents, destination
  name, privacy explanation and a clear review action. EN/DE/HU copy uses the
  requesting owner's current API culture. The logo is served from the deployed
  frontend at `/branding/averio-email-logo.png`; clients that block images still
  see the text wordmark and action.
- Configure `Email:Provider=Brevo`, `Email:BrevoApiKey`, `Email:SenderEmail`,
  `Email:SenderName`, and the deployed HTTPS `Email:FrontendBaseUrl`. Secrets belong
  in environment configuration. The development logger sender is intentionally
  not used for invitations, so raw invitation links never enter server logs;
  without Brevo, delivery is recorded as Failed. Tests use a capturing fake sender.

## API and deployment

- Owner GET/POST `/api/v1/households/{householdId}/invitations` and versioned
  `.../{invitationId}/resends` / `revocations` retain their lifecycle behavior.
- GET `/api/v1/invitations/pending`: verified recipient's valid invitations.
- POST `/api/v1/invitations/preview`: token or invitation ID, non-consuming review.
- POST `/api/v1/invitations/declinations`: recipient explicitly declines.
- POST `/api/v1/invitations/acceptances`: token or ID, `confirmed`,
  `expectedSourceVersion`, optional `successorMemberId`, and complete `accounts`
  containing `accountId`, `expectedVersion`, `openingBalance`, `handling`.
- GET `/api/v1/household-history` and `/{id}?from=YYYY-MM-DD&to=YYYY-MM-DD`:
  only the authenticated user's frozen snapshots and filtered cash flow.
- GET `/api/v1/households/{householdId}/former-members`: active members see
  display names and departure dates, without login emails or financial details.

Money in cutover review, acceptance and history is a decimal string to avoid
JavaScript precision loss. Account version tokens are opaque strings; send them
back unchanged. Cookie authentication and existing CSRF protection apply.

Apply the migration chain through
`20261009165339_AddHouseholdDepartureAndHistory` explicitly before deployment,
then deploy the API and regenerated frontend together. It adds departure/archive/
cutover timestamps, immutable JSON snapshots, and filtered active membership/owner
indexes. It retains existing records. Do not roll this migration back after use:
the old unfiltered user index cannot represent historical memberships and dropping
snapshots would destroy retained access. No production startup migration is added.

## Verification and boundaries

PostgreSQL regressions cover lifecycle security, concurrent joining and writes,
sole-holder eligibility, owner succession, exact cutover balances, no copied
movements, immutable privacy-filtered history, archive instead of deletion,
idempotency and unchanged source cash flow. Frontend regressions cover invitation
context and household reload behavior. Build, lint and generated-client checks
remain part of the normal verification workflow.

Standalone leaving/removal, live multi-household switching, history merging,
bank-feed reconnection and snapshot export remain deferred. See
[ADR-024](decisions/ADR-024-invitation-cutover-and-retained-history.md).

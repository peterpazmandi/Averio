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
card. Household settings omit the previous-households section when the history
query succeeds with no records; loading and failed requests retain explicit feedback.

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

After acceptance the frontend cancels queries, removes non-authentication queries
and clears mutation results. It keeps the mounted authentication query attached to
its observer, refreshes identity from the server, then opens the destination.
Retained membership rows are historical attribution: only `LeftAtUtc == null`
memberships in an active household determine the current user and authorization. Joined users do not run household
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
  requesting owner's current API culture. `Email:LogoUrl` is independent of
  `Email:FrontendBaseUrl`: local invitation links can stay on localhost while inbox
  image proxies fetch the public HTTPS PNG. The default logo URL is
  `https://averioapp.com/branding/averio-email-logo.png`. Deploy the frontend's
  `public/branding/averio-email-logo.png` asset at that URL, or configure a publicly
  reachable CDN PNG. The URL is validated at startup; HTTPS/public-host syntax
  does not prove deployment availability. Clients that block images still see the
  text wordmark and action. Each content block has a table row, with spacing
  on table cells. The standard review button uses a 48px table cell and full-height
  link rather than anchor padding. Text and spacer rows inherit the card's continuous
  background instead of painting separate rectangles. Dark-mode media rules exclude
  Outlook's `x_`-prefixed classes so the message-pane light/dark toggle takes
  precedence over the operating-system theme. An outer white table gives Outlook
  a color-rewrite trigger; the nested surfaces are selected through its original
  style/attribute color markers (`data-ogsc/ogsb/ogac/ogab`). This avoids relying
  on compound class/attribute selectors that Outlook does not reliably support.
  The displayed fallback URL contains only optional `<wbr>` breaks, preserving the
  credential and copyable text without invisible Unicode separators.
  Presentation tables and inline background colors provide the base layout; Outlook's Word renderer receives a fixed-width fallback
  and a VML review button. Both buttons and the plain fallback link carry the same
  escaped invitation URL. Dark-mode media and Outlook selectors adapt the navy
  surface, text and emerald accents. Actual inbox rendering still requires checking
  in Outlook and Gmail because clients may override colors. Deploy the API and
  send/resend a new invitation for manual verification: received emails retain
  their original HTML and cannot pick up a template change.
- Configure `Email:Provider=Brevo`, `Email:BrevoApiKey`, `Email:SenderEmail`,
  `Email:SenderName`, `Email:LogoUrl`, and the deployed HTTPS
  `Email:FrontendBaseUrl`. For local testing, keep `FrontendBaseUrl` on localhost
  and use the same public `LogoUrl` as production. Secrets belong
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
context, immediate active-household refresh with a mounted auth observer, cancelled
source requests, discarded finance caches, conditional history sections and household
reload behavior. Build, lint and generated-client checks
remain part of the normal verification workflow. Headless Chrome also measures
actual rendered EN/DE/HU email fixtures at desktop and 320px widths in 72 cases:
light/native-dark themes, Outlook markers on a real outer table, Outlook class
prefixing with a light message pane under OS dark mode, all four prefixed Outlook
marker variants, removed head styles, removed anchor box styles and blocked images.
Checks cover contrast, ordering, containment, continuous backgrounds, message-theme
selection, link identity, copyable URL text and button height. They also assert
that a localhost invitation link uses a separate public HTTPS logo URL before
serving the actual repository PNG over HTTP and checking decoded image dimensions.
Fixtures use synthetic credentials and make no email deliveries. These model
documented transformations in a browser; they are not Outlook emulation or a
substitute for inbox testing.

To run the email checks after a Release build from the repository root:

```sh
dotnet run --project tests/Averio.Persistence.Tests --configuration Release --no-build -- --invitation-previews /tmp/averio-email-previews
node tests/email/invitation-layout.mjs /tmp/averio-email-previews
```

Chrome must be installed (`CHROME_BINARY` can specify its executable). Fixture
output is disposable and contains no real recipients or invitation credentials.

After deploying the public image, run this separate deployment preflight with Node
24 (no emails are sent):

```sh
node tests/email/check-logo.mjs https://averioapp.com/branding/averio-email-logo.png
```

It requires successful HTTPS delivery, `image/png`, a PNG signature and nonzero
image dimensions, so a SPA's HTTP-200 HTML fallback cannot pass. Also check the
URL from a browser without authentication: Gmail/Outlook proxies must reach it
without cookies, a bot challenge, a private tunnel or localhost. Brevo's
transactional endpoint does not provide a supported CID image attachment path;
the template therefore uses a public PNG rather than data URIs or CID references.
Received emails cannot be repaired by a deployment: send/resend a fresh invitation
after updating the API and asset, then check both Outlook message-pane themes and
Gmail with remote images enabled.

Standalone leaving/removal, live multi-household switching, history merging,
bank-feed reconnection and snapshot export remain deferred. See
[ADR-024](decisions/ADR-024-invitation-cutover-and-retained-history.md).

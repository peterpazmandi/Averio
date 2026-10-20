# Household settings

The Household settings navigation opens `/app/household` after onboarding.
The page follows the canonical Household Settings HTML layout with shared
Tailwind cards, responsive section links, EN/DE/HU text and light/dark states.

## Behavior

All household members can read household name, country, preferred reporting
currency and the member directory (display name, role, UTC join date, self marker).
Login email addresses, user IDs and financial details are not directory fields.
Unnamed members have a localized fallback. A solo household displays its owner
as a real member rather than an empty invitation screen.

Only owners can edit the profile. Changing preferred currency never converts
money or changes account currencies. Members see an explanation of read-only
settings and of how financial privacy remains independent of administrative role.

Saving invalidates household settings, onboarding/shell, current-user and dashboard
queries. A draft retains its original concurrency version across background reads.
On conflict it stays visible until explicitly discarded; reloading fetches current
values. Loading and request failures are never represented as an empty member list.
After discarding a draft, editing stays disabled until the explicit reload succeeds.
If loading fails, Retry must succeed before another draft can be opened from the
refreshed settings and version.

## API and compatibility

- `GET /api/v1/households/{householdId}` retains existing profile fields and adds
  `version` and `members`.
- `PATCH /api/v1/households/{householdId}` requires `expectedVersion` plus optional
  name/country/preferredCurrency changes. Clients must first read the household.
- Owner-only changes; unknown/other households return 404, members attempting an
  update return 403, missing versions return 400, stale versions return 409.
- Database concurrency failures map to localized `household_changed` errors.
- Completed onboarding profile writes for household data return
  `household_setup_completed`; use the versioned settings endpoint instead.

## Deployment

Apply the `AddHouseholdVersion` EF migration explicitly before deploying the API.
It adds a required UUID version, backfills existing households and leaves all
financial records untouched. Deploy the regenerated frontend client with the API:
the PATCH contract now requires a version. Standard API startup does not migrate.

## Verification

Household section navigation uses a labeled native selector below 1024px and a
sticky icon sidebar on desktop. Personal Money uses the same compact selector on
small screens. Short EN/DE/HU option labels keep every choice readable; desktop
navigation retains the full labels and descriptions. Hash links and browser
back/forward select the corresponding section. Invitations remain owner-only.
Profile, invitation, Personal Money and adjustment drafts survive section switches within
the current page session; a reload still discards unsaved input.

Run `node tests/household-navigation.browser.mjs` from `apps/averio-web` for the
responsive regression matrix: 320, 375 (iPhone SE), 390, 768, 1024 and 1440px,
all three languages, light and dark. It checks navigation, permissions, keyboard
selection, draft retention, label fit, tap targets and overflow using the real
components and shell content widths with deterministic API fixtures.
Screenshots are written to `test-results/household-navigation` and uploaded by CI.
These browser fixtures complement the database-backed collaboration suite below.

The PostgreSQL suite includes household member/owner/outsider/anonymous access,
directory projection, settings normalization, invalid input, stale writes,
concurrent contexts, unchanged account currencies and closed onboarding paths.
Run it through the existing CI workflow or with `AVERIO_TEST_CONNECTION_STRING`
pointing to a disposable local PostgreSQL server (the suite creates its own DB).

Owners can create, resend and revoke invitations. Verified invitees can join from
an existing household with reviewed account copies and retained history. During
acceptance, a departing owner chooses a successor when other members remain; a
last-member household is archived read-only. ADR-024 supersedes the original
empty-household joining restriction in ADR-023. See [Household invitations](household-invitations.md)
and [Collaboration verification](collaboration-verification.md). Independent removal
or departure without an invitation remains deferred.

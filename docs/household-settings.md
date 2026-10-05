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

The PostgreSQL suite includes household member/owner/outsider/anonymous access,
directory projection, settings normalization, invalid input, stale writes,
concurrent contexts, unchanged account currencies and closed onboarding paths.
Run it through the existing CI workflow or with `AVERIO_TEST_CONNECTION_STRING`
pointing to a disposable local PostgreSQL server (the suite creates its own DB).

Invitations, acceptance, removal, leaving and owner transfer are not implemented.
Their accepted boundaries and joining eligibility are recorded in
[ADR-023](decisions/ADR-023-household-settings-and-joining-policy.md).

# ADR-023 — Household administration and safe invitation joining

**Status:** Superseded in part by [ADR-024](ADR-024-invitation-cutover-and-retained-history.md); household settings and invitation security remain accepted  
**Date:** 2026-10-05

## Context

Averio currently creates a household and owner membership during registration.
A unique user membership and repository queries assume one household per user.
Accounts, authorship, category pins, activities and assignments reference that
household/member boundary. Joining another household cannot safely be implemented
by overwriting a membership's household ID or deleting its financial history.

This decision extends ADR-001 and ADR-003. It preserves one household per user,
the household-first model and the separation of administration from financial
permissions. It does not introduce multi-household switching.

## Implemented in Household Settings (PR 1)

- Members can read their own household's profile and member directory. Requests
  outside their household return not found; anonymous requests require login.
- The directory returns membership ID, display name (nullable), role, join date
  and a self marker. It exposes neither login email/user ID nor account, balance,
  transaction, activity or other financial information.
- Only the owner can edit household name, country and preferred reporting currency.
  Shared accounts/categories retain their existing member collaboration rules.
  Household ownership never grants access to another member's private finances.
- Settings writes require the version returned by the read endpoint. Both a stale
  submitted version and a database write race produce a localized conflict.
  The frontend preserves the draft and requires explicit discard/reload.
- Preferred currency changes display preference only: they do not convert money,
  rewrite historical reports or change account currencies.
- Completed financial onboarding is not an alternative settings-write endpoint.
- Member removal, leaving, owner transfer and invitations are not executable in
  this slice. The UI must not display working-looking controls for them.

## Accepted policy for invitation implementation (PR 2)

### Administration and invitation lifecycle

Only the household owner may create, resend or revoke invitations. An invitation
grants the Member role, never ownership or private-account access. Only the owner
may inspect pending invite email addresses; accepted members use the minimal
directory described above.

Persist inviter, household, normalized target email, role, creation/expiry times,
token hash, lifecycle version, and acceptance/revocation information. Use a
cryptographically random, expiring, single-use token; never store/log raw token
material as ordinary invitation metadata. Persist only its hash for validation.
An email-delivery job that needs the raw link must protect its payload and delete
it after delivery/expiry. Duplicate pending invites for the same household/email
are rejected or explicitly resent; resending rotates the token and invalidates
the previous link. Rate limits and cooldowns apply. Delivery failure is visible
to the inviter and retryable; it must not create a membership.

States are Pending, Accepted, Revoked and Expired (expiry can be derived from
ExpiresAt). Accept and revoke are mutually exclusive atomic transitions. A GET
from an email scanner never consumes an invitation. Acceptance requires an
authenticated, email-verified user whose normalized email matches the invitation,
an explicit confirmation and CSRF protection. Before authentication, reveal no
household/member details. Authorization is repeated at acceptance time, including
inviter authority and target household eligibility.

### Joining eligibility

| Situation | Behavior |
|---|---|
| New user arriving through an invitation | Preserve invitation context through registration, verification and login. Explicit acceptance joins the target household instead of requiring solo financial setup. |
| Existing user with an empty solo household | Allow a confirmed, atomic transition only after checking all dependencies. |
| Existing user with accounts or other financial/context data | Reject safely with an explanation. Keep every record and their existing household access. |
| User already in the target household | Treat a retry of their accepted invitation as success without duplicate membership. |
| User belongs to another shared household | Reject until leaving/transfer semantics are implemented. |
| Expired, revoked, wrong-email or otherwise invalid invitation | No membership or household change. |

For the first version, an empty solo household must have exactly its owner and
no accounts (including archived accounts), transactions (including voided rows),
transfers, custom categories, labels, personal pins/preferences, activities or
assignment records. Global seeded categories/icons are not household data.
Identity/profile data and the household profile alone do not prevent joining.
Every future household/member-dependent persisted feature must extend this
eligibility check. The check must be explicit and conservative, never based only
on transaction count.

Under a transaction, lock and revalidate the invitation, source/target households
and membership in a consistent order; serialize against creation of dependent
data. All relevant write paths must participate in the locking protocol before
joining ships. Atomically consume the invitation, establish the target membership,
and remove only the verified-empty disposable source household/membership. Do not
move any financial records or depend on cascading deletion to discover eligibility.
Keep audit information about the transition. Competing acceptances must not create
multiple memberships; the existing database uniqueness invariant remains.

After acceptance, refresh identity/membership/onboarding state and cancel/remove
old household financial queries before navigating. Joining an already configured
household must not require creating a redundant first account or changing the
owner's household profile. Keep user-profile completion separate from household
financial setup where the joining flow requires it.

### Deferred operations and beta boundary

Automatic migration/merging of households with history, multiple memberships,
member removal/leaving and owner transfer require separate accepted decisions.
Do not expose destructive membership controls until authorship, account holders,
personal records, retained history and access after departure are specified.
Do not advertise unrestricted household switching or a complete membership
lifecycle. A wider collaboration release needs an explicit supported departure
and recovery process; this settings slice does not provide one.

## Consequences

Inviting a partner becomes a bounded future slice with no silent loss of existing
data. Some existing users cannot join in the initial invitation release; the UI
must explain this before acceptance rather than suggesting deletion of history.
The settings page is useful immediately and establishes an honest, privacy-aware
foundation for collaboration and eventual user research with couples.


## Implementation note — 2026-10-06

The invitation slice now implements the accepted lifecycle and joining policy.
See [Household invitations](../household-invitations.md) for endpoints, token handling,
delivery failure semantics and the PostgreSQL parent/FK locking protocol.
Email delivery is synchronous after invitation persistence; Unknown is recoverable
by an explicit resend. This does not introduce a worker or durable outbox.


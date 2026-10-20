# Averio Domain Model

**Status:** Authoritative conceptual domain reference  
**Updated:** 2026-09-19

This document captures accepted domain semantics. Some concepts are intentionally defined before their final database/entity implementation.

## Central model

```text
User
  │
  └── HouseholdMember
          │
          ▼
      Household
          │
          ├── Accounts
          ├── Transactions
          ├── Categories
          ├── Goals
          ├── Recurring rules
          └── Personal-money rules
```

Every active financial user operates in a household context. A solo user has a household of one.

## User

Authentication/person identity. Typical responsibilities: email, password/identity state, email verification, optional profile data, and timestamps.

A User is not itself the authorization object for household resources; household authorization flows through membership.

## EmailVerificationToken

Verifies email ownership. It is user-bound, expiring, and single-use. Raw secret material must not be exposed in logs.

## PasswordResetToken

Authorizes a password reset. It is user-bound, expiring, and single-use. Password reset must not expose raw token material in logs.

## Household

The collaboration and financial-context container.

Important: Household does **not** mean "owner of every financial resource in it." It provides context for membership, authorization, shared rules, reporting, collaboration, and shared resources.

Typical fields: Id, Name, selected household preferences/base reporting currency when introduced, and timestamps.

## HouseholdMember

Relationship between User and Household.

Typical fields: HouseholdId, UserId, Role, membership state, timestamps.

Initial roles conceptually include Owner and Partner/Member.

Role governs household administration. Role does not grant universal access to another member's private financial data.

### Membership cardinality

The current early product operates around one active household context per user. Avoid choices that make broader multi-household support impossible later, but do not introduce complexity before it is needed.

## Household administration (ADR-023)

ADR-023 defines owner-managed, verified, expiring email-bound invitations.
[ADR-024](decisions/ADR-024-invitation-cutover-and-retained-history.md) supersedes
its empty-household joining restriction. Users retain one active membership;
departed memberships record `LeftAtUtc` and remain attribution/FK targets.
Acceptance copies active sole-holder personal accounts with opening balances and
explicit destination handling, never prior transactions or transfers.
Household-owned/joint accounts stay behind. Original ledgers remain; last-member
households record `ArchivedAtUtc` instead of being deleted. Departing users retain
immutable, individually authorized `HouseholdHistory` snapshots of what they could
see at departure, separate from current reporting. Copied source accounts record
`CutoverAtUtc` and are permanently closed. See [Household invitations](household-invitations.md).

Household members can read a minimal directory (membership ID, display name, role,
join date and self marker). Login emails and financial details are excluded.
Only owners edit household name/country/preferred reporting currency. Settings use
an opaque UUID version for optimistic concurrency. Shared financial-resource
permissions remain independent of household administrative role.

Completed financial onboarding cannot act as an alternative profile-settings write.
Preferred reporting currency does not convert amounts or change account currencies.
See [ADR-023](decisions/ADR-023-household-settings-and-joining-policy.md).

## Invitation

An owner-managed, email-bound invitation grants the Member role only. It stores a hash of
the single-use token, has an expiry and lifecycle version, and records delivery and
acceptance/revocation metadata. Expiry is derived from `ExpiresAt`; pending invitations can
be resent (rotating the token) or revoked. Acceptance requires an authenticated, verified,
email-matched user and explicit confirmation. Joining is allowed only for a new user or a
verified-empty solo household; financial history is never silently moved or deleted.
See [Household invitations](household-invitations.md) for endpoint and lifecycle details.

## Account

A financial container such as checking, savings, cash, or credit.

Core invariants:

- belongs to one household context
- account currency is immutable
- archived instead of destructively deleted after financial history exists
- supports opening/balance-anchor semantics
- has explicit economic scope/ownership semantics
- has visibility semantics
- holder information is separate from ownership

### Account scope/ownership

Do not model `Owner = User | Household` as a simplistic polymorphic owner.

Separate:

1. household context
2. economic scope/ownership
3. individual owner-member relationships when needed
4. legal/physical account holder
5. visibility
6. reporting participation

Example:

```text
Household context: Pázmándi Household
Economic scope:    Household
Legal holder:      Péter
```

Meaning: Péter legally holds the bank account, but the money is treated as household money.

### Current Account Management implementation boundary

The current code implements Household and Personal scope, Household and Private visibility, and explicit named-holder relationships in AccountOwner. The holder relationship does not replace economic scope. This is narrower than the future visibility/reporting concepts below:

- Household scope requires Household visibility and inclusion in reports.
- Private visibility requires Personal scope and exclusion from household reports.
- Personal creation/editing keeps the acting member among the holders.
- Any member may manage household-scope accounts; personal management is limited to named holders. Household administrator status adds no personal-finance permission.
- Editing changes name, type, holders, visibility and reporting participation. Currency, economic scope and the recorded balance/date remain fixed in this flow. A future scope-transfer or balance-reconciliation use case must define its financial-history impact before exposing these changes.
- Archive/reactivate preserves the account and history. Archived accounts must be reactivated before editing.
- Edits submit the last observed UpdatedAt value; concurrent changes return a conflict. EF uses the existing timestamp as a concurrency token. New versions advance at PostgreSQL microsecond precision.
- Lists keep deterministic name/ID ordering. No account pinning or manual ordering has been adopted.

### Joint ownership

Where an account is owned by multiple individuals, represent those member-owner relationships explicitly. Do not overload `Household` as a fake human co-owner.

The final table/entity shape should be finalized in the Account vertical slice.

### Account visibility

Visibility is separate from ownership. Previously discussed useful semantics include Full, BalanceOnly, and Private; exact enum names may evolve.

- **Full:** normal authorized detail visibility
- **BalanceOnly:** limited account/balance presence without transaction detail
- **Private:** detail visible only to explicitly authorized owner/member(s)

No merchant, category, note, receipt, or other private detail may leak through shared views.

### Reporting participation

Reporting participation is independent of visibility. A private financial container may have explicitly selected anonymized budget impact, but privacy does not automatically mean inclusion or exclusion.

## Account holder

Describes who legally/physically holds an account when relevant. Holder information must not determine economic ownership by itself.

## Opening/balance anchor

Accounts have a persisted starting balance/date so users can begin tracking without fabricating all financial history before Averio.

The anchor is immutable in the normal Account Management edit flow. It is the opening balance at the start of `BalanceAnchorDate`.

**Current balance is a read model, not a second stored mutable balance.** For a requested as-of date:

```text
current balance
= balance anchor
+ posted visible income from anchor date through as-of date
- posted visible expense from anchor date through as-of date
+ posted incoming transfers in the account currency within the same window
- posted outgoing transfers in the account currency within the same window
```

Transactions before the anchor date do not change the current balance. Future-dated transactions do not change today's balance until their date is reached.

Privacy is applied before the movement is added. Household-visible transactions are included for members allowed to see the account. Private transactions are included only for an account holder who is allowed to see them. This prevents a private transaction from leaking indirectly through a shared current-balance number.

Creating, editing or voiding a transaction never rewrites the anchor. The UI invalidates account/dashboard reads so the derived balance is recalculated from the authoritative ledger.

## Money

Financial values use decimal-compatible amount + currency semantics.

Never use binary floating point for money.

Different currencies cannot be directly added.

## Currency

Initial supported codes: EUR, USD, HUF.

Account currency is immutable. Cross-currency conversion is explicit, not an automatic entity behavior.

## Transaction

An authoritative income/expense ledger entry associated with one account.

The implemented Transaction Management slice deliberately models **Income** and **Expense** only. A transaction uses the account's immutable currency and contains one or more ordered **TransactionItem** records. Its positive total is derived from the exact decimal sum of item amounts; the transaction kind supplies the cash-flow direction.

A transaction may also contain free-text Merchant/Store, optional note, date, Household/Private visibility, creator membership, status/timestamps, and zero or more reusable labels.

`Description` is a derived compact summary: Merchant/Store when available, otherwise the first item description.

### TransactionItem

A receipt or other transaction can be split into multiple items. Each item stores:

- ordered position
- description
- CategoryId
- CategoryName snapshot
- CategoryIcon snapshot
- amount in the transaction/account currency

Every item must have a valid available Category. The base Uncategorized category provides a low-friction fallback.

The Category ID is the classification identity. Name/icon snapshots preserve transaction-history presentation if the category display metadata changes later.

### Transaction labels

Transactions select reusable Label entities through a join table.

Label scope:

- **Household** — reusable throughout the household
- **Account** — reusable only on the specific account

The selected account controls which Account labels are valid. Label scope never grants transaction visibility and cannot bypass account/transaction privacy rules.

## Category

Category is a first-class classification entity for transaction items.

Core fields include Id, optional HouseholdId, optional ParentId, SystemKey or Name, kind (Expense/Income/Both), Icon, AccentColor, SortOrder, base/archive flags, and timestamps.

Categories support a parent-child tree through `ParentId`. The initial product seed contains exactly 100 system categories: 20 parent categories with four children each.

System and custom category naming are intentionally different:

- **System category:** `SystemKey` is required and `Name` is null. The database stores only a stable product localization key such as `groceries-supermarket`; it does not store an English display name.
- **Custom household category:** `SystemKey` is null and `Name` contains the user-supplied category name.

The database enforces this either/or rule. System categories are product-wide rows with no HouseholdId. Custom categories belong to one Household.

The frontend owns translations for system-category keys. English, German, and Hungarian resources contain all 100 system category names. Changing UI language therefore changes category display/search text without rewriting financial data.

Transaction item category snapshots store the stable system key for a system category or the custom name for a custom category, together with the copied icon. The live CategoryId remains the classification identity.

### Category icons

`icons` is a reusable icon chooser/catalog seeded with 240 values.

Category does **not** reference an Icon row. The chosen icon value is copied into `Category.Icon`. This deliberate snapshot rule prevents future icon-catalog maintenance from changing existing categories unexpectedly.

## Label

Label is a reusable transaction-tag catalog scoped to the household context.

A Household label has no AccountId. An Account label has exactly one AccountId. Normalized-name uniqueness is enforced inside each scope.

Transactions relate to labels through `transaction_labels`; label text/scope is not duplicated into every transaction row.

## Transfer

A transfer is paired movement between financial containers, not a normal expense/income pair.

Transfer Management now implements this through a separate, single-row aggregate owning both sides (ADR-018). Create/edit/void is atomic; accounts/currencies are immutable, amounts are positive decimals, and same-currency amounts must match. Cross-currency transfers retain explicitly entered actual settlement amounts in both currencies; their directional ratio is the effective conversion, sourced from the user's settlement information and dated by the transfer date. There is no market-rate or report-conversion service.

The actor must be able to manage both accounts. Current account visibility applies separately to each side. An inaccessible side is returned as null, and the note is hidden unless both accounts are visible. Hidden account IDs cannot be used to filter history. Voiding retains history and reverses both posted balance effects; stale edits are rejected.

Transfers contribute to each account's balance within that account's anchor/as-of window but never enter income/expense/category aggregates. Transfers across household/personal scope do not yet consume personal-money allocations or produce boundary cash-flow metrics. Fees are separate expenses.

## Reporting aggregates

Statistics are derived/read-model data, not the authoritative ledger.

Averio synchronously maintains Account and Category aggregate dimensions at Day, Month, and Year grain.

Every aggregate retains the source AccountId so current account visibility/reporting participation can be applied before values are returned. This is important for Category aggregates: a category total must never reveal spending from a private or excluded account.

Account aggregates use the transaction's full item-derived amount and count transactions. Category aggregates use item amounts grouped by directly assigned CategoryId and count categorized items.

Parent/child category rollups are intentionally not persisted multiple times. Hierarchy rollups can be composed at read time from direct category rows.

Only posted Household-visible transaction contributions enter household reporting aggregates. Ledger and aggregate mutations are atomic. Currency series remain separate and are never directly summed.

## Goal funding vs ledger movement

Moving money to a savings account linked to a goal is not automatically consumption. The goal contribution is planning semantics while the ledger movement may be a transfer.

Keep planning semantics and cash-flow semantics distinct.

## Domain relationships at a glance

```text
User 1 ── 0..1 active HouseholdMember (current product scope)
Household 1 ── * HouseholdMember
Household 1 ── * Account
Account 1 ── * Transaction
Transaction 0..* ── TransactionItem
Category 0..1 ── * child Category
Household 1 ── * Goal
Household 1 ── * RecurringRule
HouseholdMember 1 ── 0..1 PersonalMoneyWallet (initial scope)\nPersonalMoneyWallet 1 ── * PersonalMoneyEntry (immutable)
```

Exact physical cardinalities can evolve through ADRs/migrations; the semantic separations above are the important invariants.



## Category management lifecycle (ADR-019)

System category definitions are globally shared and immutable through household APIs.
Custom categories belong to one household and all its members may manage them. Names are shared data; there is no private-category scope.
The hierarchy is limited to a parent and direct children. Kind is immutable; parents must be active, available to the household, and type-compatible.
Archive/restore preserves IDs and financial history. Archive parents only after active children; restore parents before children.
Category edits use UpdatedAt concurrency. ExistingItemId in transaction input identifies a previously authorized item whose unchanged category retains its name/icon snapshot. Archived categories cannot be newly assigned.
Reports continue to use stable category IDs and current category display data; persisted totals do not change on category edits. Parent rollups remain a future read-model feature.

## Category hierarchy and preferences update (ADR-021)

Categories support deeper nesting. A compatible active subcategory may be a parent; self/descendant moves are rejected under a household lock. Personal pins carry a persisted sequence. Shared definitions and financial snapshots remain independent of these preferences.


## Personal Money — ADR-029 / ADR-030 / ADR-031

Personal Money is an independently tracked member-specific entitlement, **not** a bank account, transaction category, or extra income/expense entry. A wallet belongs to exactly one `HouseholdMember` with an immutable EUR/USD/HUF denomination. Unused amounts roll forward automatically; a completed charge can make the entitlement negative.

The append-only ledger uses signed `Grant`, `Funding`, `PrivateCharge`, `SharedCharge`, `PrivateRefund`, `SharedRefund` and `Surrender` entries. Linked refunds reference both the real Income transaction and the original purchase; shared-account charges and refunds modify entitlement without affecting attributed private funding. A `ReturnFunding` transfer is a negative Funding event from private to included household cash. Entitlement is grants plus private charges (charges are negative), while attributed private funding is funding plus private charges. `Funding` transfers do not increase entitlement a second time. Manual grants have an idempotent operation ID. Transfers with explicit purposes either create-and-fund (owner/private-holder) or fund an existing entitlement (recipient private-holder). Ordinary transfers have no wallet effect.

Existing transfer and transaction edit/void operations append corrective entries atomically. Chargeable private purchases are explicit and owner-visible only. Personal Money does not change existing categorized `TransactionAggregate` semantics. Cross-currency attribution and automatic settlement remain future slices. Monthly recurring entitlement grants are represented by `RecurringAllowanceRule`, with one append-only `Grant` event per due month linked through `recurring_allowance_rule_id`. The worker never moves bank funds, and the original membership/currency scope remains immutable. Manual shared-account charge splits, refund attribution, funding returns, surrender and monthly boundary-flow reporting are covered by ADR-030/PR 2.

See [ADR-029](decisions/ADR-029-personal-money-entitlements-funding-attribution.md), [ADR-030](decisions/ADR-030-personal-money-shared-spending-reconciliation.md), [Personal Money Foundation](personal-money.md), [Reconciliation](personal-money-reconciliation.md), [ADR-031](decisions/ADR-031-recurring-personal-money-allowances.md) and [Recurring Personal Money](personal-money-recurring.md).

## Personal activities

`Activity` belongs to one household member and has a name, optional dates, open/closed status and concurrency timestamp. `ActivityAssignment` links a visible transaction to at most one activity per member, with an independent version retained after removal. Assignments are private metadata, not ledger mutations. Results sum currently visible posted income minus expenses through today's UTC date, separately per original currency; transfers and future/voided transactions do not contribute. Closed activities reject new assignments but retain live correction/void effects. See [ADR-022](decisions/ADR-022-personal-activities.md) and [Activities](activities.md).


## Invitation lifecycle implementation

`HouseholdInvitation` belongs to the target household, grants only Member, and stores inviter, normalized email, token hash, version, timestamps, delivery status and acceptance audit. SourceHouseholdId identifies the retained source household. Accepted and Revoked are terminal; Pending expiry is derived. Acceptance and the membership transition commit atomically. Source records are retained; account copies and individually authorized history snapshots commit with departure. See [invitation implementation](household-invitations.md) for locking, constraints and cutover semantics.



## Recurring rules and occurrences (ADR-025)

A RecurringRule belongs to one household member and account, with a category, exact decimal amount, income/expense kind, transaction visibility, original date, cadence, optional end date and version. Rule visibility is always personal. Its next date is derived from the original anchor and occurrence index. Pause retains dates; stop is terminal.

A RecurringOccurrence receipt is uniquely identified by rule/date and belongs to the same household. A recorded receipt references a ledger transaction and original recorded amount; a skipped receipt references neither. The receipt, ledger, aggregates and next date commit atomically. Correcting or voiding a transaction never reopens its receipt. History filters current transaction visibility.

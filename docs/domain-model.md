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

## Invitation

Planned concept for inviting another user/member into a household.

Expected concerns: inviter, target email, intended role, status, secure token, expiry, acceptance, cancellation/revocation, and duplicate-pending-invitation rules.

Not assumed implemented yet.

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

Transfers are intentionally outside the first Transaction Management slice. They must be implemented as linked movements with atomic creation/update/void behavior so they cannot be double-counted or orphaned. Cross-currency transfers additionally require explicit FX semantics.

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
HouseholdMember 1 ── * PersonalMoneyPeriod/Allocation (future implementation)
```

Exact physical cardinalities can evolve through ADRs/migrations; the semantic separations above are the important invariants.

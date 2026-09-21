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

Accounts need a defined starting balance/date so users can begin tracking without fabricating all history before Averio.

The final persistence model may use an opening/anchor balance plus date and subsequent ledger movement.

## Money

Financial values use decimal-compatible amount + currency semantics.

Never use binary floating point for money.

Different currencies cannot be directly added.

## Currency

Initial supported codes: EUR, USD, HUF.

Account currency is immutable. Cross-currency conversion is explicit, not an automatic entity behavior.

## Transaction

An authoritative income/expense ledger entry associated with one account.

The implemented transaction slice deliberately models **Income** and **Expense** only. A transaction uses the account's immutable currency and contains one or more ordered **TransactionItem** records. Its positive total amount is derived from the exact decimal sum of those item amounts; the transaction kind supplies the cash-flow direction.

A transaction may also contain:

- free-text Merchant/Store
- optional note
- date
- Household or Private visibility
- creator membership
- status and timestamps
- Household-scoped or Account-scoped labels

`Description` on the transaction is a derived summary used for compact list/search scenarios. Merchant/Store is preferred when present; otherwise the first item description is used.

### TransactionItem

A receipt or other transaction can be split into multiple items. Each item currently stores:

- ordered position
- description
- optional category-name snapshot
- amount in the transaction/account currency

The Category vertical slice is not implemented yet, so item category is intentionally a name snapshot rather than a fake Category entity/reference. This allows useful itemization now while leaving category hierarchy, icons, ordering and ownership semantics for the dedicated Category implementation.

### Transaction labels

A transaction may have labels with explicit scope:

- **Household:** shared contextual label; no account id is stored
- **Account:** label tied to the transaction's selected account

The UI visually distinguishes the scopes. This slice stores scoped labels on the transaction; reusable label catalogue/autocomplete management can be introduced later.

Transaction visibility is constrained by the containing account. A private transaction on a household-visible personal account is visible only to that account's holders. Household-scope accounts do not accept private transactions. Transactions on a private account remain private through the containing account boundary.

Posted transactions may be corrected. Historical ledger rows are not hard-deleted: removal from active cash flow is represented by a **Voided** status. Optimistic concurrency prevents silent overwrites.

Changing a transaction to another account is permitted only when the target account uses the same currency. Moving an existing transaction between currencies would imply conversion semantics and is therefore rejected.

## Transfer

A transfer is paired movement between financial containers, not a normal expense/income pair.

Transfers are intentionally outside the first Transaction Management slice. They must be implemented as linked movements with atomic creation/update/void behavior so they cannot be double-counted or orphaned. Cross-currency transfers additionally require explicit FX semantics.

## Reporting aggregates

Statistics are derived/read-model data, not the authoritative ledger.

Averio incrementally maintains day- and month-grain cash-flow aggregates keyed by household, account, currency, period kind and period start. Only posted, household-visible income/expense contributions enter these aggregate rows, and mutation of the ledger and its aggregate delta happens inside one database transaction.

Reporting queries still apply the account's current household visibility/reporting participation before returning totals. Currencies are grouped independently and are never directly summed.

This gives dashboard/statistics queries work proportional to requested periods/accounts instead of raw ledger size, while preserving the ability to rebuild aggregates from the authoritative transaction table if repair tooling is needed later.

## Goal funding vs ledger movement

Moving money to a savings account linked to a goal is not automatically consumption. The goal contribution is planning semantics while the ledger movement may be a transfer.

Keep planning semantics and cash-flow semantics distinct.

## Reporting aggregates

Statistics are derived/read-model data, not the authoritative ledger.

Potential dimensions include period, category, account, merchant/store, and household/member scope.

Private data must be filtered or intentionally anonymized before aggregation results reach a consumer.

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

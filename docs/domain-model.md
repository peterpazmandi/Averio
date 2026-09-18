# Averio Domain Model

**Status:** Authoritative conceptual domain reference  
**Updated:** 2026-09-16

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

A ledger event recorded against one or more financial containers.

Conceptual kinds:

- Income
- Expense
- Transfer

Typical data includes account, date/time, amount, currency, merchant/store, category, note, optional items, visibility/privacy, recurring linkage, receipt linkage, and timestamps.

## Transfer

A transfer is paired movement between financial containers, not a normal expense/income pair.

Transfer linkage must prevent double counting and orphaned counterpart movements.

Cross-currency transfers require explicit FX semantics.

## Transaction privacy

Transaction visibility can become more restrictive than account-level visibility where needed, but must never bypass the account's privacy boundary.

Private details are server-filtered before presentation/aggregation.

## Transaction item

Optional receipt/import line item. May include description, quantity, unit price, total, and later category details.

Itemization is not required for ordinary manual transactions.

## Category

Hierarchical classification with parent/subcategory support.

Conceptual fields/behaviors:

- ParentCategoryId
- Name
- Icon
- optional accent color
- SortOrder
- Pinned
- household/personal semantics when required

System category localization must be separated from user-created names.

## Recurring rule

Represents expected repeated activity. It is not itself a completed transaction.

A future system may distinguish user-created recurring rules from automatically inferred recurring patterns.

## Goal

A savings/financial target. Goals may be household/shared, personal, or private.

Potential data: scope/owner, name, target amount/currency, target date, funding relationships, visibility, status.

A private goal must not leak its target or activity without explicit sharing.

## Personal money

Personal money is a household-budget sub-ledger for individual autonomy.

Conceptually:

```text
Allocation
+ Carry-over
- Chargeable personal spending
= Available personal money
```

Example:

```text
Allocation: +€300
Spend:       -€50
Spend:       -€80
Spend:       -€20
----------------
Available:   €150
```

Unused amount carries into the next period by default.

### PersonalMoneyAllocation

An allocation changes budget responsibility. It is not automatically a household expense.

If €300 is allocated while money remains in a shared account, household cash has not left. Counting both the allocation and later purchase as expenses would double-count.

### Crossing into a private account

When real household cash moves into a private financial boundary, household reporting may intentionally stop at that boundary. Subsequent private merchant purchases must then not be counted again as separate household expenses.

This preserves privacy and prevents double counting. The exact implementation should be finalized before the full personal-money feature is coded.

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

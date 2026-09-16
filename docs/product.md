# Averio Product Definition

**Status:** Authoritative product reference  
**Updated:** 2026-09-16

## Product summary

Averio is a household-centric personal finance and budgeting application. It is built around the reality that household finances are often neither completely shared nor completely separate.

Couples and households commonly have a mixture of shared money, personal money, legally individual but economically shared accounts, private spending, shared bills, shared goals, and several currencies. Averio should model those realities directly rather than forcing users into an "everything private" or "everything shared" model.

## Vision

Averio should make it natural to start alone and later collaborate with a partner or household without rebuilding financial history.

Every user therefore operates inside a household context from the beginning. A solo user starts with a **household of one**. Adding another member evolves that context rather than migrating the user to a completely different data model.

## Target users

Primary users include:

- couples managing some or all finances together
- households with a mix of shared and individual accounts
- users who want budgeting without giving up financial privacy
- European/multi-currency households
- people dissatisfied with rigid shared-finance models
- users replacing spreadsheets with a focused household-finance tool

Averio is not currently intended to be a brokerage, bank, tax-filing system, or accounting ERP.

## Product principles

### Household-first, not household-forced

The household is the collaboration context. It must support shared and private financial states at the same time. Joining a household must never mean "all my finances become visible."

### Personal autonomy inside shared finance

Members can participate in shared planning while retaining private financial space. Household administration and financial visibility are separate concerns.

### Privacy is a first-class product feature

Users should understand who can see an account, balance, transaction, goal, or aggregate. Private data must not leak indirectly through statistics, category totals, exports, search, or notifications.

### Financial semantics before pretty metrics

Averio must prefer correct money semantics over impressive dashboards:

- transfers are not ordinary expenses
- allocations are not automatically expenses
- currencies cannot be added without conversion
- legal holder and economic ownership are different
- private spending must not be double-counted

### Explain, don't overwhelm

Reporting should eventually answer "what changed and why?", not only render charts.

## Core product areas

### Identity and authentication

Core foundation:

- registration
- email verification
- resend verification
- login
- logout
- forgot password
- reset password
- cookie-based sessions

Planned:

- two-factor authentication
- Google login
- Facebook login
- active-session management

### Onboarding

Onboarding establishes the first financial context after authentication.

Expected content:

- optional full name
- optional country
- optional gender if the field remains useful
- household setup/context
- first account
- currency choice
- later goal/preferences setup

Progress should be resilient and not lose completed work due to navigation or connection interruptions.

### Dashboard

The first main page after onboarding.

It should eventually show:

- household/account balance context
- comparison to prior period
- income vs expenses
- month-to-date cash flow
- spending by category
- goals
- latest transactions
- upcoming recurring commitments
- personal-money status
- first-time-user guidance

An aggregated "Bento" dashboard endpoint is acceptable when it materially improves load performance.

### Accounts

Initial account types include checking, savings, cash, and credit.

Account UI must communicate:

- name/type
- currency
- economic scope/ownership
- legal/physical holder where relevant
- visibility
- balance
- archive state
- household reporting participation

Account currency is immutable after creation.

Initial supported currencies: EUR, USD, HUF.

### Transactions

Transactions should support:

- income
- expense
- transfers
- merchant/store
- category
- date
- note
- items when available
- household/private visibility
- recurring linkage
- personal-money attribution
- receipt attachment/scanning later

Transactions form a financial ledger and should not be destructively deleted casually.

### Recurring transactions

Support scheduled/expected items such as rent, utilities, subscriptions, salary, personal-money allocations, and goal contributions.

A later version may distinguish explicit recurring rules from recurring patterns inferred from history.

### Categories

Support parent/subcategory hierarchy, icons, optional accent color, pin/unpin, drag-to-reorder, and household/personal semantics where needed.

Category management should stay fast and approachable.

### Goals

Goals may be household/shared, personal, or private.

Examples: emergency fund, family holiday, home improvement, personal purchase.

Shared goal functionality must not expose unrelated private account data.

### Statistics and insights

Support income, expenses, savings rate, categories, trends, account/currency context, goal contributions, and household/member scopes.

Server-side privacy filtering applies before data reaches the browser.

Advanced human-readable insights can become premium, while core statistics remain useful without AI.

### Household management

Household settings own:

- household identity
- members
- invitations
- roles
- privacy defaults
- personal-money rules
- relevant shared settings

Membership is distinct from account ownership.

### Personal money

Personal money gives each household member discretionary autonomy inside the household budget.

Track:

- periodic allocation
- amount used
- amount remaining
- rolled-over amount

Unused amount rolls over by default. Personal money is a budget sub-ledger, not merely an expense category.

### Historical import

Planned capabilities:

- CSV import
- XLSX import
- multi-tab workbook support where useful
- account/category mapping
- validation/review before commit
- duplicate handling
- migration of long financial history

### AI receipt scanning

Planned differentiating workflow:

1. upload/photo receipt
2. extract merchant, amount, date, and items
3. propose categorization/account details
4. user reviews
5. user confirms the transaction

AI prepares data; the user remains in control of the ledger.

## Multi-currency semantics

Multi-currency is a domain concern, not a formatting option.

A transaction retains its original currency. An account has one immutable currency. Averio must never treat `100 EUR + 100 USD` as a meaningful `200`.

Normalized reports require an explicit target/base currency, exchange-rate source, effective rate date, and transparent conversion semantics. Original amounts remain inspectable.

## Household/private finance model

Keep these concepts distinct:

- membership: who belongs to the household
- scope/ownership: whose money a resource economically represents
- holder: who legally/physically holds an account
- visibility: who can inspect it
- reporting participation: whether/how it affects household metrics

The UI should explain these concepts in ordinary language rather than exposing raw domain jargon.

## Monetization direction

Averio should become useful and trustworthy before monetization becomes aggressive.

Natural premium surfaces include:

- AI receipt scanning
- advanced AI-assisted insights
- automatic bank connections
- advanced rules/automation
- richer exports/reports
- enhanced historical import/mapping
- selected collaboration capabilities

Core/free value should still provide secure identity, household-of-one support, basic account management, manual transactions, categories, core reporting, and essential privacy behavior.

Exact packaging and pricing are not yet decided and must not be invented during implementation.

## Growth and validation

Household invitations can create a natural growth loop because one active user can invite another member, but collaboration usefulness comes before acquisition tactics.

Reddit and similar communities are used primarily for customer research, privacy/shared-finance validation, competitor insight, and careful beta-user recruitment—not spammy promotion.

## Brand and UX

Primary brand color: `#10B981`.

Averio should feel modern, calm, trustworthy, financially serious without looking like traditional online banking, and approachable to non-experts.

Languages: English, German, Hungarian.  
Themes: light/dark.  
Layouts: responsive/mobile-first where appropriate.

The approved HTML UI concept is the visual source of truth until explicitly superseded.

## Current non-goals

Do not prematurely optimize for:

- microservices
- Kubernetes-scale operations
- enterprise RBAC
- brokerage/investment trading
- tax filing
- autonomous AI financial decisions
- dozens of currencies before EUR/USD/HUF semantics are correct

The near-term goal is a trustworthy household-finance foundation that a solo developer can realistically build and operate.

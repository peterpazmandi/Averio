# Personal Money foundation (ADR-029)

**Status:** PR 1 implementation slice. See [ADR-029](decisions/ADR-029-personal-money-entitlements-funding-attribution.md) for accepted invariants, permissions and later slices.

## Meaning

Personal Money is a budget entitlement attached to a specific active household membership, not a bank account. Each membership initially has at most one wallet in EUR, USD or HUF. Its denomination is immutable even if the household preferred reporting currency changes.

The source of truth is a signed, append-only event stream:

| Kind | Entitlement balance | Attributed private funding | Example |
|---|---:|---:|---|
| Grant | +amount | no effect | Owner grants €300 |
| Funding | no effect | +amount | Household → private €300 |
| PrivateCharge | −amount | −amount | Explicit private purchase €50 |
| Correction of Grant/Funding/PrivateCharge | opposite signed effect | according to kind | Editing or voiding source |

A negative entitlement remains visible and carries into the next period. There is no month-end rollover write; the balance is calculated from the complete entry stream.

`AttributedPrivateFunding` is **not** the private bank account balance. Private accounts can hold unrelated income, savings or expenses.

## Manual v1 API

### Owner grants

`POST /api/v1/households/{householdId}/personal-money/grants`

Example request:

```json
{
  "recipientMemberId": "00000000-0000-4000-8000-000000000001",
  "currency": "EUR",
  "amount": "300.00",
  "occurredOn": "2026-10-17",
  "operationId": "00000000-0000-4000-8000-000000000002"
}
```

Only the household Owner can issue a grant; retrying with the same operationId does not create another grant. Reusing an operationId with changed amount or date conflicts. The grant alone does not move cash or create a categorized expense.

### My wallet

`GET /api/v1/households/{householdId}/personal-money/me`

The response includes owner-only ledger entries, entitlement, attributed private funding and the difference. A nonconfigured wallet returns 204. Other members cannot request someone else's private spending history through this endpoint.

### Private spending consumption

`POST /api/v1/households/{householdId}/personal-money/me/charges`

```json
{ "transactionId": "00000000-0000-4000-8000-000000000003", "amount": "50.00" }
```

An active member may explicitly link a posted **private** expense on an account they can manage. The wallet denomination and expense currency must match, and the charge must not exceed the expense. Ordinary private expenses remain uncharged. Updating or voiding a linked transaction reverses the charge in the same database transaction; an updated expense needs explicit relinking.

### Transfer purpose

`POST /api/v1/households/{householdId}/transfers` now has optional `personalMoneyPurpose`:

- `null` or `Ordinary`: original behavior; no entitlement/funding entry.
- `CreateAndFundGrant`: only for Owner transferring household money into **their own** exclusively held private account, creating one linked grant and one funding entry.
- `FundExistingGrant`: any eligible recipient (as their own private account holder) may fund an already-approved grant without increasing its entitlement.

All usual transfer rules still apply: write authority over both accounts, no cross-household transfer, two legs posted atomically, and source/destination currency settlement. Personal Money purposes additionally require identical wallet and account currency, Household-scope included source, Personal-scope Private excluded destination, and single-member private ownership.

A household Owner cannot select another member's private account under the current account authorization model. Instead they can grant an allowance and let the destination holder create the linked transfer.

Transfer edit/void rebalances linked grants and funding through new signed events, not by rewriting the original. This does not generate any ordinary expense transaction.

## Privacy and reporting

- Grant authority is household-level; private consumption is member-only.
- Members who can view the shared source transfer already see that leg. No private destination/account/merchant/item details are returned to them.
- No private purchase is materialized into the household transaction timeline as an "anonymous" exact-date/amount row.
- Existing `TransactionAggregate` continues to represent the ordinary authorized income/expense stream. Do not add grant totals, private charges or transfer funding to those aggregates.
- A distinct boundary-cash-flow metric is required later for a complete household "what left shared control?" report.

## Technical integration

- Domain: `PersonalMoneyWallet`, `PersonalMoneyEntry`, `PersonalMoneyBalance`.
- Application: `PersonalMoneyHandler`, `IPersonalMoneyRepository`.
- Infrastructure: `PersonalMoneyRepository`, EF mappings, migration and snapshot.
- Transfers: optional explicit funding purpose, create/edit/void corrections in the original transfer PostgreSQL transaction.
- Transactions: update and void reverse any linked private charge in the original transaction.
- Contracts/API: grants, wallet read and private charge operations.
- Tests: domain ledger invariants and PostgreSQL integration assertions.

## Limitations / next PRs

This is **not** a complete personal-money user journey until the remaining PR work is implemented and verified:

- Shared-account personal charges and partial household-purpose splits.
- Refunds, funding returns, surrender and overfunding settlement.
- Household boundary-transfer reporting and private-aware dashboard visualization.
- Wallet information in immutable previous-household snapshots.
- Multi-currency attribution and exchange-rate policy.
- Scheduled grants and any future bank automation.
- A dedicated dashboard/standalone Personal Money page and full browser end-to-end scenarios. The initial household allowance panel, transfer funding selector, generated OpenAPI client, and EN/DE/HU UI strings are included in PR 1.

The ordinary transfer dashboard cash-flow and category aggregates remain unchanged intentionally. Run the .NET transaction and PostgreSQL checks (including EF pending-model check), update generated OpenAPI client, and finish private-account browser verification before merging.

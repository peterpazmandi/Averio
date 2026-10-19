# Personal Money: shared spending and reconciliation (PR 2)

**Architecture:** [ADR-029](decisions/ADR-029-personal-money-entitlements-funding-attribution.md) and [ADR-030](decisions/ADR-030-personal-money-shared-spending-reconciliation.md).  
**Scope:** Manual transaction attribution, explicit refunds, transfers that return funding, entitlement surrender, and separate household-boundary reporting. **No scheduled allowances** (PR 3).

## Two reporting dimensions (never double count)

The existing ordinary income/expense ledger and `TransactionAggregate` remain authoritative for bank purchases. A Person Money charge or refund is a **spending-purpose attribution** and does not post another financial transaction.

| Event | Wallet entitlement | Attributed private funding | Actual household flow |
|---|---:|---:|---|
| Grant €300 | +300 | 0 | 0 |
| Fund private account €300 | 0 | +300 | €300 leaves included accounts |
| Shared-card purchase €80 marked personal | −80 | 0 | €80 normal expense |
| Private purchase €50 marked personal | −50 | −50 | No second household outflow |
| Refund of shared purchase €20 | +20 | 0 | Recorded original bank income only |
| Refund of private purchase €10 | +10 | +10 | Private-account income only |
| Return private funding €40 | 0 | −40 | €40 returns to included accounts |
| Surrender €30 entitlement | −30 | 0 | No movement |

A fully privately funded allowance can remain overfunded after spending from shared accounts; the **funding difference** may become negative. It is a tracking/reconciliation indicator, **not** a bank balance or proof of the legal ownership of funds.

## Operations

### Shared-account purchase

The existing POST `/api/v1/households/{householdId}/personal-money/me/charges` and create-transaction `chargePersonalMoney` option now support a posted household-visible expense on an included Household account; the member must have account write permission. Partial amounts are supported through the manual attribution API. Both private and shared linked charges can be edited or unlinked using the existing optimistic PUT endpoint with `amount, expectedAmount, expectedVersion, operationId`. A shared charge reduces **only** wallet entitlement and is never included again in household expense totals. Sum of live Personal Money charges across wallets cannot exceed the source expense amount.

### Refund a charged purchase

POST `/api/v1/households/{householdId}/personal-money/me/refunds`

```json
{
  "refundTransactionId": "00000000-0000-4000-8000-000000000001",
  "purchaseTransactionId": "00000000-0000-4000-8000-000000000002",
  "amount": "20.00",
  "operationId": "00000000-0000-4000-8000-000000000003"
}
```

Both referenced transactions already exist. The refunded purchase must have an effective charge in the actor's wallet, and the refund must be a posted Income on the same account and in the same currency/visibility. Its amount cannot exceed the refund income or the original effective wallet charge after other explicit refunds. An Income transaction can be attributed only once. Retry the same operation ID and payload; changed payloads conflict. Source income or original purchase edits/voids reverse linked refund attributions atomically.

- Private refund: restores entitlement and attributed private funding.
- Shared-account refund: restores entitlement only.

The first release does **not** automate merchant matching or support cross-account/cross-currency refunds. Both may be designed later.

### Return attributed private funding

The ordinary transfer POST accepts `personalMoneyPurpose: "ReturnFunding"` **only** for an authorized, same-currency transfer from an exclusively owned Personal/Private/excluded account to a Household/included account. It posts a **negative Funding** event linked to the transfer. Initial return must not exceed positive attributed private funding; edits revalidate increases. Source account/recipient permissions are identical to the existing paired transfer rules. Edits and voids post date-aware compensating Funding events; the original grant remains intact. An ordinary private-to-household transfer is not automatically a funding return.

### Surrender unused entitlement

POST `/api/v1/households/{householdId}/personal-money/me/surrenders`

```json
{
  "amount": "30.00",
  "occurredOn": "2026-10-19",
  "operationId": "00000000-0000-4000-8000-000000000004"
}
```

Only the active wallet holder may surrender up to a **positive available** entitlement. The operation is idempotent and recorded as a negative `Surrender` event; **no money moves**. To return physical funds too, make a separately confirmed transfer with ReturnFunding. No recurring grants or automatic settlements are introduced.

### Monthly boundary cash-flow report

GET `/api/v1/households/{householdId}/statistics/boundary-flow?from=2026-10-01&to=2026-10-31`

Returns `months` grouped by calendar month and currency, reporting `payoutsToExcludedAccounts` and `returnsToHousehold`. Only **posted** transfers that cross from included household reporting accounts to excluded/private accounts (or back) are included. Transferring between two included household accounts does not count. A regular crossing transfer appears even without Personal Money tagging; funding grant amounts/usage are shown elsewhere and cannot be added to bank expense totals. The endpoint returns **no private account ID/name, merchant, note or individual transfer timeline**.

This is deliberately a distinct read model rather than adding transfer amounts to `TransactionAggregate`.

## Security and lifecycle

- Household Owner status does not override a member's private wallet or private account access.
- Shared purchase details stay visible to members under existing account rules; attribution does not hide the underlying shared bank transaction.
- Refunds link only transactions the acting wallet holder may access and manage; membership changes invalidate future operations.
- Source transaction edits/voids retract active charge and dependent refunds in the same PostgreSQL transaction.
- Entry corrections are append-only; retries use operation IDs. Household row locking serializes budget grants, charges and returns with mutation commands.
- Old household wallets remain linked to original membership; privacy-filtered history-snapshot exposure is deferred.

## Verification / follow-up

Run `dotnet build Averio.slnx --configuration Release`; run transaction domain checks and PostgreSQL integration suite with the existing `AVERIO_TEST_CONNECTION_STRING`; run the frontend typecheck, lint, browser tests and `api:generate` drift check in CI. Confirm historical snapshot privacy separately, and ensure no double counted `TransactionAggregate` rows on shared purchases.

**Explicit limits:** cross-currency reconciliation, same-bank refund matching across accounts, split allocations across multiple members in a single editor, automatically detected refunds, automatic settlement, and scheduled allowances (PR 3) are outside this slice.

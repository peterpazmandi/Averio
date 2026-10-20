# ADR-030 — Shared Personal Money charges, returns and reconciliation

**Status:** Accepted for PR 2  
**Date:** 2026-10-19  
**Extends:** ADR-004, ADR-018, ADR-029; does not change account ownership, ordinary transfer semantics or the recurring schedule rules.

## Decision

Personal Money is the member's spending entitlement, **not cash or a virtual bank account**. All mutations remain append-only. PR 2 adds separate **SharedCharge**, **PrivateRefund**, **SharedRefund**, **Surrender** entry kinds and a **ReturnFunding** purpose for a real private-to-household transfer.

| Event | Entitlement | Attributed private funding | Bank ledger |
| --- | ---: | ---: | --- |
| Shared Charge of expense | decreases | unchanged | original shared expense only |
| Private refund on linked purchase | increases | increases | original posted private income only |
| Shared refund on linked purchase | increases | unchanged | original posted household income only |
| Funding return | unchanged | decreases | one existing paired private→household transfer |
| Surrender unused entitlement | decreases | unchanged | no transfer |

The existing `PrivateCharge` reduces both entitlement and attributed funding. Historical correction events retain each kind. Unspent and negative entitlement carry over automatically, without monthly entries.

### Shared-account attribution

Only an active member may assign **their own wallet** to a posted, authorized, household-visible expense paid from an active Household-scope account; normal household transaction visibility remains unchanged. Attribution may cover part or all of an expense. Sum of effective charges across wallets cannot exceed the expense total. The transaction remains recorded once in `TransactionAggregate`; a wallet charge is a **secondary budget-purpose dimension**, never additional income or expense.

The account and transaction must match the wallet's immutable currency. An update or void of the underlying transaction atomically reverses its current wallet effect on each occurrence date. Correcting or unlinking a shared charge uses the existing `amount / expectedAmount / expectedVersion / operationId` optimistic, idempotent contract, including after category/item changes.

### Refund policy

A refund is an existing, **posted Income transaction** on the **same account** and in the **same currency** as the original charged expense. A member explicitly links it to their own previously charged purchase; it does not follow from income automatically. Distinct income transactions can refund the same purchase in parts; total active linked refunds per member may not exceed the currently effective Personal Money charge on that purchase. One income transaction can be attributed at most once. Private purchases require the original holder's authorization and stay private; no other member (including the household Owner) sees that linkage.

The refund ledger entry stores both its own Income `TransactionId` and `RelatedTransactionId` for the original expense, without cloning merchant information. Updating or voiding either source reverses the attribution in the same transaction. Refunds restore entitlement and, only when paid to the private funding account, the attributed private funding. They are not a second cash inflow.

### Funding returns

`ReturnFunding` is a transfer purpose available only for authorized, same-currency transfers from the member's exclusively held Personal/Private/excluded account to an included Household account. It posts one **negative Funding** entry linked to the real transfer. It never revokes a grant. A return must not exceed **positive attributed private funding** at posting; later edits that increase its amount revalidate the remaining attributed funding. Editing or voiding the return corrects funding attribution with signed, date-aware entries. An ordinary reverse transfer is **not** automatically a return of funding.

### Surrender

The member can explicitly surrender up to their **positive available entitlement**, using an operation ID to prevent duplicate requests. Surrender creates a negative `Surrender` entry; it never moves account balances, cannot bypass wallet ownership, and cannot consume a negative wallet further. A bank transfer and a surrender are separate confirmed operations.

### Reconciliation and reports

Attribution is not a claim about the real private bank balance. Report distinct measures: available entitlement; attributed private funding; funding difference; shared and private consumption; and real household **boundary cash movement**. A dedicated, **monthly and currency-separated** boundary-flow projection totals posted transfers from included household accounts to excluded/private accounts and vice versa. The projection does not expose private destination/source IDs, notes, merchant names, or transaction-level timing. Ordinary transfers entirely within the household reporting boundary do not appear; ordinary transfers crossing the boundary appear whether or not earmarked Personal Money.

Existing category and cash-flow aggregates remain untouched. Boundary payouts, grant amounts and wallet charges are **not summed into an existing categorized expense**. For shared-card consumption already funded privately, the wallet's funding difference becomes negative until excess funding is returned; the application must label it, not silently imply that the private balance equals the entitlement.

### Concurrency and membership/privacy

Mutations acquire the existing household transaction lock (and existing transfer account locks where relevant), then revalidate active membership, account scope, source status and authorization. Replay with the same operation ID is safe and conflicting payloads are rejected. Wallet endpoints expose only the acting member's complete charge/refund/settlement details. Reporting endpoints show monthly household boundary totals only, across included funds that authorized members can already inspect. Historical wallets stay scoped to the old membership; PR 3 is solely for scheduled allowances.

### Explicit initial limits

Refund attribution requires the same source account, so cross-account/multi-currency reimbursements are deferred. Shared charge assignment in PR 2 is initiated by the member whose entitlement is used; no member may charge another wallet. Historical household cutover snapshots require a separate privacy-aware extension before publishing wallet history for departed memberships. Dates are reporting dates, not bank settlement guarantees.

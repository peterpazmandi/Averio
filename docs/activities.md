# Activities

Activities answer questions such as “Did the St. Ingbert trip finish in plus or minus?”
They group existing income and expenses without changing account balances or categories.

## Use

1. Open **Activities** under Planning and create “St. Ingbert”. Dates are optional.
2. Choose the activity when creating or editing a transaction. You can also create an activity from the picker.
3. For existing entries, open Transactions, choose **Select transactions**, select up to 100 posted entries, then assign them together. Choose no activity to remove assignments.
4. Record reimbursements as income and assign them to the same activity. A EUR 620 reimbursement and EUR 575 expense produce EUR 45 net income.
5. Review income, expenses and net separately for each currency. Open the linked transaction list to inspect the history across all dates.
6. Close the activity when finished. Reopen it before adding more entries. Closing does not freeze financial history: correcting or voiding an existing entry updates the result.

Activities and their names are personal to the current household member. Another member may independently group the same visible transaction. Each member can assign a transaction to at most one activity. Changing an assignment never edits the shared ledger or grants access to a private account.

## Interpreting the result

Choose **Transaction chart** on any activity to see income and expenses grouped by transaction date. Select a currency to view its bars independently. Each pair of bars represents one date with eligible transactions; dates without transactions are omitted, so horizontal spacing is categorical rather than elapsed time. Long timelines scroll horizontally. Hover a bar for its amount or expand **View exact daily amounts** for the accessible table (also available on touch screens). The chart works for open and closed activities and follows the same actual-result and visibility rules below.

- Net = posted income − posted expenses through today (UTC).
- Future-dated and voided entries remain available in the linked history but do not contribute to the result.
- Activity dates describe the activity; they do not limit which assigned transactions count.
- EUR, USD and HUF are never added together. There is no converted grand total.
- Transfers are not income or expense and cannot be assigned.
- Only currently visible entries contribute. Losing visibility immediately removes their details, counts and amounts from the result.
- A personal activity can include visible private entries and entries excluded from household reporting.
- No eligible entries means no currency result, rather than an invented zero in an arbitrary currency.

This release tracks actual amounts, not promised reimbursements. Split a combined salary/reimbursement into separate transactions if only reimbursement belongs to the trip. Partial transaction allocation, shared activities and expected amounts are deferred (ADR-022).

## API and concurrency

Under `/api/v1/households/{householdId}`:

- `GET /activities` returns personal activities with exact decimal strings per currency.
- `GET /activities/{activityId}/timeline` returns daily income, expenses and counts by original currency, ordered by transaction date. It verifies personal activity ownership and current ledger visibility, and excludes future/voided transactions. Amounts are exact decimal strings; the browser only converts scaled bar coordinates to numbers. Data loads when the chart dialog opens and shares activity cache invalidation after ledger/assignment changes.
- `POST /activities` creates an activity.
- `PUT /activities/{activityId}` updates details/status with `expectedUpdatedAt`.
- `PUT /activities/assignments` atomically assigns/removes 1–100 visible posted transactions, each with `expectedVersion`.
- `GET /transactions?activityId={id}` filters personal assignments while enforcing existing visibility and pagination.

Transaction responses include personal `activityId`, `activityName` and `activityAssignmentVersion`. Create/update accepts optional `activityAssignment`; omitted or null preserves assignments. To remove an assignment, provide a non-null input with `activityId: null` and its current version. A never-assigned entry has the all-zero GUID version. Unassigned rows retain a new version to reject stale writes. Assignment versions are independent of ledger `updatedAt`.

Conflicts require refreshing current data before retrying. Bulk failure rolls back the entire selection. Database foreign keys enforce household/member consistency; member row locks serialize assignment and close/reopen writes.

## Deployment and verification

Apply the `20261003103809_AddActivities` EF migration explicitly before API deployment using the existing migration procedure. It adds `activities` and `activity_assignments`; no existing money, aggregates or ledger entries are rewritten. Rolling the migration back deletes activity metadata, so preserve it before any rollback. Normal API startup does not apply production migrations.

The PostgreSQL persistence suite exercises exact decimals, separate currencies, future exclusion, edit/void recalculation, private metadata, visibility changes, independent member assignments, closed activity rules, atomic bulk rollback, stale writes, concurrent requests and database ownership constraints. Run it with `AVERIO_TEST_CONNECTION_STRING` pointing at a disposable PostgreSQL server (the suite creates and removes its own database), alongside the account and transaction suites. Run frontend regression tests, build, lint and OpenAPI regeneration as defined in `.github/workflows/verify.yml`.

Manual UI checks: create an activity; assign expense and reimbursement; inspect the net and linked history; bulk move/remove assignments; close/reopen; switch language (EN/DE/HU); check narrow screens, dark mode, keyboard dialog navigation, loading/error/empty states; verify another household member cannot see activity names.

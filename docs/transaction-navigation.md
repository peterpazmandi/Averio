# Transaction filters and detail links

Transactions use validated URL search parameters so bookmarks, refresh and browser Back/Forward restore the same view.

| Parameter | Meaning |
| --- | --- |
| `account` | Accessible account UUID; account links include `dates=all` |
| `kind` | `Expense` or `Income`; omitted means both |
| `from`, `to` | Inclusive ISO calendar dates (`YYYY-MM-DD`) |
| `dates=all` | Disable implicit current-month bounds; explicit dates still apply |
| `activity` | Existing activity UUID filter; defaults to all dates |
| `transaction` | Transaction UUID to fetch and open independently of the list |

Without date parameters, the list retains its current-month default. Month selection writes explicit bounds. Clearing either date supports open-ended ranges using `dates=all`. A reversed range shows a localized validation message and does not request the list. Invalid dates, UUIDs and enum values are discarded. Free-text search and unsaved editor fields remain local.

Opening a transaction adds its ID while preserving the list filters. Back closes it, and Forward reopens it. Closing a detail opened from the list returns to the previous history entry; closing a direct or refreshed link removes only the transaction parameter. Dashboard recent transactions link directly to the record. Account cards and dashboard account names link to that account's all-date list.

Editable records use the existing editor. Read-only and voided records use an accessible detail dialog. Missing or inaccessible IDs show a generic, dismissible error with Retry. The existing household-scoped API remains the authority for access. Detail loading does not depend on pagination or matching the current filters. A detail ID takes precedence over the existing one-shot create intent.

The interface uses shared design primitives and EN/DE/HU translations. No database migration or API contract change is required.

## Verification

Frontend tests cover search validation, leap dates and detail/create precedence. The collaboration browser suite uses a dedicated real-API household to verify URL filters, refresh, Back/Forward, records outside the filtered list, dashboard/account links, voided details, missing/foreign IDs and localized mobile/desktop dialogs. Screenshots are included in the CI browser artifacts.

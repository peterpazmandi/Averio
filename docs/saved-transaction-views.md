# Saved transaction views and relative dates

## User behavior

Transactions support named, personal saved views containing account, income/expense type, activity, selected labels and the date selection. Save current view creates a view; Update view explicitly replaces its filters; Rename changes only its name; Delete removes only the view. Adjusting filters displays Modified without silently overwriting the view. Text search, editor drafts, selected transaction, bulk selection and pagination are not saved. Applying a view clears local text search so it does not silently narrow the saved result.

Views belong to the authenticated household membership. They persist on the server and work across devices. Another member, including a household owner, cannot list, update or delete them. Switching household or signing out clears cached view data along with other financial queries.

Labels are existing reusable labels. The filter catalog contains household labels and account labels only for accounts visible to the caller, including archived accounts. All selected labels must be present on a transaction. Filtering happens in PostgreSQL after privacy restrictions and before pagination. Up to 20 labels may be selected. Account labels keep their account scope; they do not become household labels.

The transaction page keeps filters in a modal (a bottom sheet on phones), so opening filters does not push the timeline down. Filter changes remain live in the URL; Show results or Escape closes the dialog. The first four saved views appear as one-click chips; Manage views lists all views and provides apply, update, rename and delete actions. Saving and management explanations stay inside dialogs.

## Date and URL contract

The `period` parameter preserves the selection, not the dates resolved today. UTC calendar boundaries match the dashboard. Resolved dates are visible in the From/To controls alongside the preset selector. They refresh on page focus and within 30 seconds of a UTC day change.

| Value | UI label | Inclusive bounds |
| --- | --- | --- |
| `this-month` | This month | First of current month through today |
| `last-month` | Last month | Entire previous calendar month |
| `previous-3-full-months` | Previous 3 full months | Three complete months preceding the current month |
| `this-year` | This year | January 1 through today |
| `last-year` | Last year | Entire previous calendar year |
| `last-12-months` | Last 12 months | Current month through today plus previous eleven months |
| `all` | All dates | No bounds |
| `custom` | Custom range | Fixed `from`/`to`, either may be absent |

For example, opening `?period=previous-3-full-months` on November 15 resolves August 1–October 31. Opening the same saved view in December resolves September 1–November 30. Custom ranges do not move. An explicit preset takes precedence over legacy fixed dates; malformed values are discarded. Legacy `dates=all`, `activity`, `from` and `to` links continue to work. URL labels are validated, deduplicated and sorted UUIDs.

## Persistence, access and concurrency

`GET/POST /api/v1/households/{householdId}/saved-transaction-views` lists/creates views. `PUT/DELETE .../{viewId}` requires the last observed version. Updates and deletes reject stale versions with localized 409 errors; the dialog retains its original version even if a background query refreshes. Names are trimmed, limited to 80 characters and unique ignoring case within the membership. A technical cap of 50 views per membership bounds storage and UI size; it is not a paid tier.

The table has a membership foreign key, unique name index, name check, timestamps and an optimistic-concurrency version. Filters are a bounded, validated JSON payload with a stable explicit schema. Reference IDs deliberately are not cascading foreign keys: a lost reference must remain detectable. Listing marks the view unavailable when its account, activity or labels cannot be accessed. Applying it preserves the filters and shows a warning; missing IDs are never silently removed to broaden results. Saving an unavailable combination is rejected. A view can still be deleted.

Apply `20261013210249_AddSavedTransactionViews` through the normal deployment migration process before deploying this API/UI. No production database is migrated by this PR. Rollback drops saved views only, never transaction records. The generated TypeScript client must match the updated OpenAPI schema.

## Verification

Unit tests cover leap years, year boundaries, full-month semantics, UTC boundaries, reopening relative versus custom dates, URL validation and label normalization. The real-API browser suite covers label filtering before pagination, CRUD, cross-session restore, refresh/history, duplicate names, stale writes/deletes, same-household and cross-household privacy, lost account/label access, and EN/DE/HU responsive dialogs. Existing transaction-detail and dashboard regressions remain included.

## Possible paid features — ideas, not implemented or committed

Keep basic filters, date presets and personal saved views free. Potential paid extensions are scheduled summaries for a saved view, threshold/change alerts, richer exports and comparison reports, and automation based on saved criteria. A later collaboration offering could support explicitly shared views, but sharing a view must never grant access to another person's private finances.

Validate demand before building these. Candidate measures are repeat saved-view use and requests for recurring reports. Scheduled delivery requires consent, delivery preferences, unsubscribe controls and privacy checks at send time; an account or membership losing access must stop sensitive content from being included. No billing, notifications, email delivery or background scheduling is added here.

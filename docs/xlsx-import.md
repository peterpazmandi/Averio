# Money manager XLSX import

Transactions → Import XLSX opens a four-step flow: upload, account mapping, category mapping, and review/confirmation. It supports the Hungarian Money manager & expenses export (`ru.innim.my_finance`) with Kiadások, Bevétel and Átutalás sheets. This is a deterministic adapter; financial records are not sent to an AI provider.

## Data handling

- Maximum 5 MiB compressed, 32 MiB expanded, 10,000 records; formulas, external workbook relationships, unknown headers and unsupported currencies/formats are rejected.
- Dates use the export's date-only Excel serials. Amounts remain decimals on the server and strings over the API.
- Source accounts must map one-to-one to active, writable accounts with matching currencies. Create missing accounts directly from each source row in a modal. The source name/currency are prefilled, the opening balance must be supplied, and the new account is automatically selected. These accounts are created immediately and remain if the import is cancelled.
- Map categories to existing categories or explicitly retain the source names as new shared household categories. New category names are visible to household members even if the corresponding transactions are private; the mapping screen explains this.
- Source tags become merchant text. Notes and original category names are retained. Repeated identical rows are preserved. Transfers create one transfer with two account legs, never an income/expense pair.
- Missing receiving amounts are inferred only for same-currency transfers. Unsupported original/reporting currency conversions are rejected rather than discarded.
- Private/personal accounts default to private transactions; shared personal accounts can opt into household visibility. Household accounts require household visibility. All authorization is revalidated at confirmation.
- Imports do not create recurring rules or personal-money budget charges.

## Atomicity and retries

The server stores a normalized preview in `import_batches`, scoped to the uploading household member. A SHA-256 fingerprint of sorted normalized records preserves multiplicity and ignores source row order. Re-uploading the same content by the same member returns the existing batch. The review also compares visible posted transactions and transfers across files, including visible records entered by another member.

Confirmation takes the existing household lock and commits categories, transactions, transfers, statistics and completion together. Concurrent confirmations serialize; a completed batch returns its existing result. Aggregate updates use a single set-based SQL upsert. Uploading/preparing a preview does not add financial records.

The normalized source payload is retained for the batch's lifetime, accessible only to its uploader while an active member. The original workbook is not stored. Completed imports are available on the Import history page. Draft previews can still be recovered by selecting the same file. There is no batch deletion/retention policy, arbitrary column mapping.

## Balance references

Importing never changes account balance reference amounts/dates. Existing balance rules apply: transactions on/after the reference date affect current balances; older transactions contribute to history/statistics only. Review account reference dates and existing overlapping transactions before confirming. The final screen requires acknowledgement. Compare balances after importing.

## Verification and rollout

Apply the `AddXlsxImports`, `ImportHistoryTracking` and `TransactionImportOrigin` EF migrations through the normal deployment workflow before enabling the new client. No production migration is run automatically by this feature.

`Averio.Transactions.Tests` covers parser precision, repeated rows, source fields, and malformed/unsupported inputs. `Averio.Persistence.Tests` covers concurrent preview/confirmation, uploader privacy, invalid mapping rollback, database totals and balance anchors using disposable PostgreSQL.

To check an actual export locally without committing private data:

```bash
dotnet run --project tests/Averio.Transactions.Tests -- --import-workbook /path/to/export.xlsx
```

The supplied export was parsed with 5,293 expenses (52,901,871 HUF), 284 income records (58,365,193 HUF), 39 transfers (7,626,872 HUF) and 68 repeated rows. Personal source data is not included in the repository.


## Import history and undo preview

The import page links to `/app/import-history`; completion links directly to that batch. The history lists completed imports belonging to the current member in the current household, newest first (10 per page). It shows the filename when available, completion date, source date range, counts and original totals grouped by kind and currency. Transfer totals are sent amounts, not spending. Detail pages show 50 original source rows at a time, target account/category names as mapped during import, and transaction links where still accessible. Transfers link to the transfer overview.

New confirmations atomically store generated record IDs and their initial revisions alongside the completed batch. Revisions are normalized to PostgreSQL microsecond precision. The record-state preview compares those revisions with currently accessible records, distinguishing unchanged, edited (including archiving and allocation changes that advance the revision), already voided, inaccessible and untracked records. Current IDs and revisions are withheld when access has been lost; transfers require access to both accounts. Original uploaded source data remains available only to the uploader.

Older batches have no reliable record links. They retain their source rows and totals but are marked untracked: the application never guesses links by matching dates or amounts. Batches without saved record links cannot be undone. The executable undo plan additionally checks dependencies, permissions and balance effects; unchanged revision alone is insufficient. Preview remains read-only.

History and preview browser coverage includes English, German and Hungarian at mobile/desktop widths, error recovery, pagination, hidden links, old batches and first-use states. Persistence checks cover precision, member isolation, revision changes, voiding, lost access and legacy fallback.

## Reconciliation, overlap decisions and safe undo

- Completion shows actual transaction/transfer counts, distinct affected account count and skipped count. Skipped source rows remain in the immutable payload and history, but do not contribute to imported totals or create financial records. Categories used only by skipped rows are not created.
- Account balances can be compared with an expected balance from the old app at a selected date, both after completion and in history. Decimal differences are calculated exactly. Reference amounts/dates are displayed with an explicit link to account editing; reconciliation never writes a balance adjustment. Dates before the reference date are not suitable for reconciliation.
- Overlap review matches visible posted records by mapped account, date, kind and amount. Transfers require both accounts and both amounts. Description differences do not suppress warnings. Up to three existing records are displayed per source row, with the full matching count; the user must keep or skip each flagged incoming row. Legitimate repetitions remain separate. No fuzzy or automatic deduplication is performed.
- Confirmation repeats overlap detection under the household lock. A review token captures candidates and mappings; changed matches require a fresh review. Only flagged rows can be skipped. Reviews with more than 100,000 transactions or transfers in the mapped date range ask for a smaller export.
- Successful imports retain mapping identifiers in their existing record-link JSON. Suggestions use the most recent 20 completed imports for the same uploader and household, and discard inaccessible, archived or otherwise unavailable targets. No new schema migration is needed for these five improvements. Legacy links continue to work; older imports cannot provide mapping suggestions until identifiers have been stored.
- The uploader can preview and confirm a partial undo. Only unchanged, writable records with reliable saved links and no activity, personal-money (including refund/reversal history), receipt or recurring dependencies are eligible. Dependency descriptions and owners are never exposed. Both transfer accounts must remain writable. A stale review token rejects the entire operation before changes.
- Undo uses the household lock and one transaction to subtract eligible transaction aggregates and void eligible transactions/transfers. It never hard-deletes records, accounts or categories, never changes balance reference values, and leaves blocked records intact. The preview shows today's balance effects in each account's own currency; future-dated records may affect later balances. Records predating reference dates can have zero current-balance effect.
- Repeating undo cannot reverse a record twice. A lost response may require refreshing the plan; already-voided records are retained. Uploading the same file after undo still opens the original completed batch rather than silently recreating it.

Persistence checks cover explicit keep/skip, original multiplicity, exact reconciliation, mapping reuse, uploader isolation, stale undo, linked-record protection and aggregate reversal. Browser checks exercise mapping reuse, retry preservation, accurate balances and undo confirmation in EN/DE/HU at 375px and 1440px.

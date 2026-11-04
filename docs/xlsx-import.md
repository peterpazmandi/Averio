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

The server stores a normalized preview in `import_batches`, scoped to the uploading household member. A SHA-256 fingerprint of sorted normalized records preserves multiplicity and ignores source row order. Re-uploading the same content by the same member returns the existing batch. This is not cross-file overlap detection or cross-member duplicate detection.

Confirmation takes the existing household lock and commits categories, transactions, transfers, statistics and completion together. Concurrent confirmations serialize; a completed batch returns its existing result. Aggregate updates use a single set-based SQL upsert. Uploading/preparing a preview does not add financial records.

The normalized source payload is retained for the batch's lifetime, accessible only to its uploader while an active member. The original workbook is not stored. Completed imports are available on the Import history page. Draft previews can still be recovered by selecting the same file. There is no batch deletion/retention policy, executable undo or arbitrary column mapping.

## Balance references

Importing never changes account balance reference amounts/dates. Existing balance rules apply: transactions on/after the reference date affect current balances; older transactions contribute to history/statistics only. Review account reference dates and existing overlapping transactions before confirming. The final screen requires acknowledgement. Compare balances after importing.

## Verification and rollout

Apply the `AddXlsxImports` and `ImportHistoryTracking` EF migrations through the normal deployment workflow before enabling the new client. No production migration is run automatically by this feature.

`Averio.Transactions.Tests` covers parser precision, repeated rows, source fields, and malformed/unsupported inputs. `Averio.Persistence.Tests` covers concurrent preview/confirmation, uploader privacy, invalid mapping rollback, database totals and balance anchors using disposable PostgreSQL.

To check an actual export locally without committing private data:

```bash
dotnet run --project tests/Averio.Transactions.Tests -- --import-workbook /path/to/export.xlsx
```

The supplied export was parsed with 5,293 expenses (52,901,871 HUF), 284 income records (58,365,193 HUF), 39 transfers (7,626,872 HUF) and 68 repeated rows. Personal source data is not included in the repository.


## Import history and undo preview

The import page links to `/app/import-history`; completion links directly to that batch. The history lists completed imports belonging to the current member in the current household, newest first (10 per page). It shows the filename when available, completion date, source date range, counts and original totals grouped by kind and currency. Transfer totals are sent amounts, not spending. Detail pages show 50 original source rows at a time, target account/category names as mapped during import, and transaction links where still accessible. Transfers link to the transfer overview.

New confirmations atomically store generated record IDs and their initial revisions alongside the completed batch. Revisions are normalized to PostgreSQL microsecond precision. The read-only undo preview compares those revisions with currently accessible records, distinguishing unchanged, edited (including archiving and allocation changes that advance the revision), already voided, inaccessible and untracked records. Current IDs and revisions are withheld when access has been lost; transfers require access to both accounts. Original uploaded source data remains available only to the uploader.

Older batches have no reliable record links. They retain their source rows and totals but are marked untracked: the application never guesses links by matching dates or amounts. No destructive endpoint is included. An unchanged revision does not establish that undo is safe: linked activities, allocations, balance effects and concurrency revalidation must be handled before executable undo is introduced. The preview does not change records, categories, accounts, aggregates or balance anchors.

History and preview browser coverage includes English, German and Hungarian at mobile/desktop widths, error recovery, pagination, hidden links, old batches and first-use states. Persistence checks cover precision, member isolation, revision changes, voiding, lost access and legacy fallback.

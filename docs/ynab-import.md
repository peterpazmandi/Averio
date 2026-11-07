# YNAB register migration into Averio

The YNAB adapter uses Averio's existing import engine: member-scoped previews,
account and category mapping, overlap decisions, atomic confirmation, import
history, balance reconciliation, imported-record badges and safe partial undo.
Importing is not connected to YNAB and does not send finance data to an AI model.

## Export and upload

1. In the YNAB **web** app, open your plan menu and choose **Export Plan**.
2. Upload the exported **Register** CSV or TSV. A YNAB ZIP export containing
   exactly one register and a separate budget CSV/TSV is also supported. Do not
   upload the **Budget** CSV on its own.
3. Choose the currency of the YNAB plan (EUR, USD or HUF), date order, and
   decimal separator. YNAB's register does not include reliable account
   currency columns. These settings are intentionally not inferred from the
   browser locale or the uploaded filename.
4. Map each distinct YNAB source account to an existing writable account of
   the selected currency, or create a new account. Map source categories, review
   balances and overlapping transactions, and confirm.

The adapter recognizes the standard register column sequence:

```
Account,Flag,Date,Payee,Category Group/Category,Category Group,Category,Memo,Outflow,Inflow,Cleared
```

CSV quotes, quoted embedded newlines, UTF-8 BOM, and TSV exports are supported.
The parser uses invariant dates and a user-selected decimal convention, retaining
exact decimal values with up to four fractional digits. Dates are date-only.

## Financial semantics

- Outflow/inflow map to expenses/income; negative values reverse the direction,
  e.g. refunded expenses. A record must have exactly one nonzero net leg.
- The payee becomes Averio's merchant/description field. The original category
  group and category become a single **source mapping label** (`Group / Category`),
  without silently creating a nested category hierarchy. Memos, flags and
  YNAB clearing status are retained in the transaction note. These are
  historical annotations, not Averio's own cleared-status workflow.
- Complete internal transfers are exported with two opposite account-side
  entries. They are paired by account names, date and exact positive amount.
  A pair creates **one** Averio transfer. Multiple identical legitimate
  transfers retain their multiplicity.
- A transfer with only one visible leg is rejected rather than being converted
  into a spurious expense or income. For selected-transaction exports, include
  both transfer entries or export the full plan instead.
- The CSV alone cannot reliably reconstruct parent/child **split transactions**,
  scheduled rules, account types, budget targets, category notes or other
  YNAB-specific planning state. Split-marked rows are rejected rather than
  double counted. Exports with less-obvious split encoding need a real
  YNAB fixture before extended support is considered safe.
- Accounts must map one-to-one, visibility and write permissions are rechecked
  on confirmation, and balance anchors are not modified.
- YNAB rows named **Starting Balance**, **Manual Balance Adjustment** or
  **Reconciliation Balance Adjustment** are skipped. They represent account
  equity/opening-balance corrections rather than income or spending. Set the
  Averio account balance anchor separately; importing these rows as ledger
  transactions would double-count the opening balance.
- The register identifies accounts by their exported display name. Keep source
  account names distinct when possible; YNAB exports do not provide a stable
  account identifier that Averio can use for mapping.
- Re-import and cross-file duplicate handling retain the existing Averio review
  semantics. Financial write operations are transactional; safe partial undo
  works the same way as for Money Manager imports.

## Limits and testing

Files have a 5 MiB upload limit, at most 10,000 source data rows and a
32 MiB ZIP expanded-size limit. ZIPs must contain exactly one recognizable
transaction register. Suspicious ZIP paths, invalid UTF-8, unknown headers,
invalid amounts, malformed CSV, incomplete transfers and ambiguous split rows
are rejected. A file containing only skipped balance-adjustment rows is rejected
because it contains no ledger transactions. No private fixtures are committed.

Run the transaction regression suite:

```bash
dotnet run --project tests/Averio.Transactions.Tests
```

Use a disposable integration household to test a real YNAB export; confirm
the number of transfers, exact per-currency totals, and account balances before
using the importer with production money data. No database migration is needed
for this adapter because it reuses `import_batches` and existing record links.

## Deferred work

- Proper split-transaction reconstruction from verified YNAB sample files.
- Cross-currency YNAB account representation (requires per-account currency
  metadata unavailable in standard register files).
- Optional mapping of YNAB category groups into Averio parent categories.
- Scheduled payments, savings targets, budget allocation migration and
  direct YNAB API integration, if product demand justifies them.

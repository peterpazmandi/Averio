# Transaction foundation verification

Run the dependency-free transaction regression suite from the repository root:

```sh
dotnet run --project tests/Averio.Transactions.Tests
```

It covers transaction totals and validation, category snapshots, reusable
labels, version advancement, void semantics, visibility rules, and the EF model
for categories, icons, labels, transaction details, and dimension aggregates.

## PostgreSQL integration suite

The provider-specific behavior is covered by a separate executable suite. Pass
it a PostgreSQL server connection whose user may create temporary databases:

```sh
export AVERIO_TEST_CONNECTION_STRING='Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres'
dotnet run --project tests/Averio.Persistence.Tests
```

The suite creates a uniquely named `averio_hardening_*` database, applies every
migration, performs its checks, and drops the database in a `finally` block. It
does not clear or reuse the database named in the connection string.

The integration checks verify:

- the migration chain through `20260921234000_LocalizeSystemCategories`
- exactly 100 localized base categories and 240 catalog icons
- rollback of the ledger write when an aggregate write fails
- persisted items, labels, search, balances, and currency-separated aggregates
- reversal/reapplication of aggregate buckets on edit
- stale transaction-version conflicts
- aggregate and balance reversal plus idempotency on void
- private-account and private-transaction isolation for a non-owner member
- PostgreSQL enforcement of the `updated_at` optimistic concurrency token

GitHub Actions runs both suites, the account regression suite, the frontend
build/lint, and generated-client drift verification for every pull request to
`develop`.

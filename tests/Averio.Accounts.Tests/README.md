# Account regression checks

Run from the repository root:

```sh
dotnet run --project tests/Averio.Accounts.Tests
```

This is a dependency-free executable regression suite because the repository had no test project or framework. It returns a nonzero exit code on the first failed assertion. It is included in the solution build, but must be executed with the command above; dotnet test does not discover these checks.

Covers domain invariants, update authorization, stale edits, archive/restore, holder validation, onboarding prerequisites, exact decimal validation, update-contract shape, EF concurrency metadata, holder tracking and microsecond version advancement.

Handler tests use fake repositories and a fake unit of work. EF model/tracking checks use the real Npgsql provider without opening a connection. These checks do not claim database integration coverage. Before release, verify on a disposable PostgreSQL database:

1. Apply all migrations, including AccountOptimisticConcurrency (metadata-only; empty Up/Down).
2. Create two household members and personal private, personal shared and household accounts.
3. Confirm list/detail exclude another member's private account, including archived accounts; direct IDs must return 404.
4. Confirm household administrator cannot update/archive/reactivate another member's visible personal account (403).
5. Add/remove holders and confirm unchanged rows are retained and removed rows disappear atomically.
6. Save concurrently from two contexts; the second writer must receive a conflict, with holder changes rolled back.
7. Edit while another context archives or revokes access; verify no partial write.
8. Verify FK/check constraints and original decimal precision after a round trip.

Frontend checks performed with a mocked API in a headless browser: active/archived lists, read-only personal account, precise decimal create, edit, fixed scope/currency, archive/reactivate, localized conflict propagation, preservation of failed edits, mobile overflow and absence of runtime errors. This validates the UI/transport path, not end-to-end database behavior.

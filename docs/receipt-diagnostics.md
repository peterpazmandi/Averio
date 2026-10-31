# Receipt diagnostics

Receipt upload and extraction run in different processes. HTTP 202 / `Queued` means the API stored and queued the image; it does not prove that a Worker is running. The API does not extract receipts itself.

## Follow one scan

1. Open browser developer tools, select Console, enable Preserve log, and filter for `[Averio receipts]`. Include Verbose/Debug to see each inbox poll and capability request. Normal info/warning logs include file selection (without filename), upload start/result, operation ID, scan ID, status/attempt changes, failures and a warning for pending receipts older than two minutes. The waiting warning repeats at most every two minutes per scan while the inbox receives updates; its age is since upload, not time in the current status. It is diagnostic, not proof of a failed worker.
2. Search API logs for the `OperationId` from `photo.selected` / `upload.started`. The expected sequence is upload started, admitted, storage write started/completed, queued, upload completed. The API maps the operation ID to `ScanId`. Retrying the same upload logs `upload replay` and does not consume another scan. A failed browser request includes a safe error code, HTTP status and the Problem Details `traceId` when supplied by the API.
3. Search **Worker logs**, not only API logs, for the scan ID. The expected sequence is claimed, storage read, provider, validation/persistence, processing completed. Claim logs include operation ID, attempt and queue age. Completion includes elapsed time, outcome (`ready`, `retry`, `failed`, `fenced`) and next retry time when applicable. Provider logs include HTTP status and response timing; storage failures include HTTP status and recognized S3 error codes. Scopes correlate provider/storage logs with the scan.

## When it stays queued

| Evidence | Next check |
| --- | --- |
| No `Receipt worker starting` message | Start/restart the separately deployed Worker and inspect its own stdout/service logs. A startup/DI/configuration failure may appear before the receipt service starts. |
| `ProcessingEnabled=False` / processing disabled warning | Set `Receipts__Enabled=true` in the **Worker** environment and restart it. Enabling the API alone does not enable the Worker. |
| Storage/provider configuration reported false | Supply the Worker's receipt storage and Gemini settings through its environment. API user-secrets/appsettings are not automatically inherited by the Worker. Actual secrets/bucket names are never logged. |
| Heartbeat repeatedly reports zero claims | Compare API and Worker database/environment configuration privately. Also check expiry, active household membership, future retry time and outstanding leases. Zero means no *eligible* rows, not necessarily an empty table. |
| Queue poll failed | Inspect the logged exception type/HResult and database availability/configuration. Receipt logs intentionally omit raw exceptions and SQL/connection-string-bearing messages. |
| Claimed, then storage failure | Check the Worker credentials, bucket/region and private read permissions. `AccessDenied`, `NoSuchBucket`, `NoSuchKey`, `ExpiredToken` and signature errors are distinguished. |
| Provider HTTP error / timeout | Inspect status, stage and retry outcome. The HTTP budget is 50 seconds and overall processing budget 60 seconds. Retry logs give the next attempt time; the durable queue owns the three-attempt limit. |
| Result persistence failed / fenced | Check database health or concurrent discard/expiry. A stopped Worker can leave a lease until it expires; the queue can recover it. |
| Backend logs `ready`, browser remains queued | Inspect `inbox.poll.failed`, browser network/visibility state and the API/database deployment being queried. Refresh the inbox. |

For local development, run the Worker in its own terminal after supplying its normal database, AWS and Gemini configuration:

```powershell
$env:Receipts__Enabled = 'true'
dotnet run --project apps/Averio.Worker
```

Do not repeatedly upload the same receipt while investigating a queue problem: accepted new operations consume quota. An existing queued record can be picked up once the correctly configured Worker is running.

## Logging levels

API and Worker defaults enable receipt Information logs and single-line UTC timestamps with scopes. The Worker reports startup configuration **booleans**, explicit disabled states, successful-processing events, and a heartbeat roughly once per minute after a successful poll. The heartbeat is not a separate health probe: a blocked operation delays it. Empty-poll details and individual browser HTTP polls are Debug to avoid printing the same state every three/ten seconds.

For a temporary detailed backend trace, set these values in the process you are diagnosing, then restart it:

```powershell
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Application.Receipts', 'Debug', 'Process')
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Infrastructure.Receipts', 'Debug', 'Process')
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Worker.ReceiptWorker', 'Debug', 'Process')
```

Use the equivalent environment variables in your deployment. Existing logging overrides still take precedence. Restore Information after investigation. Browser logs stay in that browser's console; this change does not add a remote telemetry endpoint.

Logs contain operational metadata only: IDs, statuses, attempts, durations, sizes and safe error codes. They exclude image bytes, filenames, hashes, merchant/item text, amounts, provider bodies, raw exception messages, API keys, bucket/object paths, cookies and connection strings. Treat correlation IDs as internal diagnostic metadata when sharing logs. No database migration or queue behavior change is required.

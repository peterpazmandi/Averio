# Receipt diagnostics

The API stores each upload before signaling its receipt extraction service. The service checks PostgreSQL at startup, after an upload signal, and when a stored retry or processing lease becomes due. An empty queue has no polling timer. The Worker handles image cleanup at 03:00 Europe/Budapest.

## Follow one scan

1. Open browser developer tools, select Console, enable Preserve log, and filter for `[Averio receipts]`. Include Verbose/Debug to see each inbox poll and capability request. Normal info/warning logs include file selection (without filename), upload start/result, operation ID, scan ID, status/attempt changes, failures and a warning for pending receipts older than two minutes. The waiting warning repeats at most every two minutes per scan while the inbox receives updates; its age is since upload, not time in the current status.
2. Search API logs for the `OperationId` from `photo.selected` / `upload.started`. The expected sequence is upload started, admitted, storage write started/completed, queued, upload completed. The API maps the operation ID to `ScanId`. Retrying the same upload logs `upload replay` and does not consume another scan. A failed browser request includes a safe error code, HTTP status and the Problem Details `traceId` when supplied by the API.
3. Search **API logs**, for the scan ID. The expected sequence is claimed, storage read, provider, validation/persistence, processing completed. Claim logs include operation ID, attempt and queue age. Completion includes elapsed time, outcome (`ready`, `retry`, `failed`, `fenced`) and next retry time when applicable. Provider logs include HTTP status and response timing; storage failures include HTTP status and recognized S3 error codes. Scopes correlate provider/storage logs with the scan.

## When it stays queued

| Evidence | Next check |
| --- | --- |
| Missing `Receipt extraction started in the API` message | Check API startup logs and the receipt configuration for that environment. |
| `Receipt extraction is disabled in the API` | Set `Receipts__Enabled=true` in the API environment and restart it. |
| API startup reports invalid receipt configuration | Supply the API's S3 bucket and region and its Gemini key and model. |
| A scan is still pending | Check the scan expiry, active household membership, the next retry time and any outstanding lease. A scan with a live lease becomes eligible after the lease expires. |
| An upload is interrupted | Uploads that remain unqueued after five minutes show a failed status. Select the photo again or enter the expense manually. |
| `Receipt queue recovery failed` | Check database availability and the API's connection settings. The receipt service backs off for 30 seconds before retrying this operation. |
| Claimed, then storage failure | Check the API credentials, bucket/region and private read permissions. `AccessDenied`, `NoSuchBucket`, `NoSuchKey`, `ExpiredToken` and signature errors are distinguished. |
| Provider HTTP error / timeout | Inspect status, stage and retry outcome. The HTTP budget is 50 seconds and overall processing budget 60 seconds. Retry logs give the next attempt time; the durable queue owns the three-attempt limit. |
| Result persistence failed / fenced | Check database health or concurrent discard/expiry. A stopped API can leave a lease. The extraction service checks its deadline after startup. |
| Backend logs `ready`, browser remains queued | Inspect `inbox.poll.failed`, browser network/visibility state and the API/database deployment being queried. Refresh the inbox. |

For local development, start the API after supplying its database, AWS and Gemini configuration:

```powershell
$env:Receipts__Enabled = 'true'
dotnet run --project apps/Averio.Api
```

Replaying a receipt with the same upload operation ID preserves the existing scan. New operations consume quota. Startup recovery picks up eligible existing queued scans.

## Logging levels

API and Worker defaults enable receipt Information logs and single-line UTC timestamps with scopes. Use API logs for upload and extraction. Use Worker logs for nightly receipt cleanup.

For a temporary detailed backend trace, set these values in the process you are diagnosing, then restart it:

```powershell
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Application.Receipts', 'Debug', 'Process')
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Infrastructure.Receipts', 'Debug', 'Process')
[Environment]::SetEnvironmentVariable('Logging__LogLevel__Averio.Api.Receipts', 'Debug', 'Process')
```

Use the equivalent environment variables in your deployment. Existing logging overrides still take precedence. Restore Information after investigation. Browser logs stay in that browser's console; this change does not add a remote telemetry endpoint.

Logs contain operational metadata only: IDs, statuses, attempts, durations, sizes and safe error codes. They exclude image bytes, filenames, hashes, merchant/item text, amounts, provider bodies, raw exception messages, API keys, bucket/object paths, cookies and connection strings. Treat correlation IDs as internal diagnostic metadata when sharing logs.

# Receipt diagnostics

The API stores each upload before signaling its receipt extraction service. The service checks PostgreSQL at startup, after an upload signal, and when a stored retry or processing lease becomes due. An empty queue has no polling timer. Receipt cleanup runs separately in the Worker at 03:00 Europe/Budapest.

## Trace a scan

1. In browser developer tools, filter the console for `[Averio receipts]` and find the scan ID. The inbox refreshes while there are pending scans.
2. In **API logs**, search for that scan ID. A successful upload logs admission, storage write and queueing. Extraction logs the claim, image read, Gemini request and result persistence. Completion reports `ready`, `retry`, `failed` or `fenced` and includes the next attempt time if applicable.
3. In **Worker logs**, check the nightly maintenance messages when investigating image cleanup.

| Symptom | What to check |
|---|---|
| Missing `Receipt extraction started in the API` message | Inspect API startup and check `Receipts__Enabled`, the S3 bucket and region, and the Gemini key and model. |
| `Receipt queue recovery failed` | Check database availability and the API's connection settings. The service retries recovery after a database error. |
| Claimed, then storage or provider failure | Check the API's private S3 read permissions and Gemini settings. The logged stage, timing and error code identify the failed operation. |
| Processing persists after an API restart | An interrupted scan can retain its two-minute processing lease. Startup recovery waits for the stored deadline before reclaiming it. |
| API logs `ready` and the browser still shows a pending scan | Inspect `inbox.poll.failed`, browser connectivity and whether the frontend uses the expected API. |

For local extraction, configure receipt scanning in the API and start `apps/Averio.Api`. Use `Logging__LogLevel__Averio.Api.Receipts=Debug` to adjust extraction service verbosity. Logs omit raw receipt contents and credentials.

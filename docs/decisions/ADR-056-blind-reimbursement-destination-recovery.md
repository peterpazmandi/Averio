# ADR-056 - Recover inaccessible reimbursement destinations

Status: Accepted

Date: 2026-11-25

Extends ADR-053 with a narrow destination-recovery operation. Ordinary claim and historical payment visibility remain unchanged.

The current claim creator may replace an inaccessible destination without viewing the hidden account or claim details. The recovery list returns only opaque claim IDs and revisions, at most 100 at a time. It includes only non-cancelled claims with no active payment links and a source transaction the creator can still view and modify. Active links must first be removed through payment recovery. Historical credit visibility remains required for ordinary claim access, so changing the destination does not necessarily reveal the claim.

The write rechecks membership, source visibility and write access, claim ownership and revision, the old destination's invisibility, and that the replacement account is visible, active, modifiable and uses the claim currency. It records before/after audit events without changing financial transactions, amounts or submission state. Old audit-event destination IDs are omitted from API responses when the account is no longer visible.

An unconfirmed response requires refreshing the recovery list before another attempt. The revision prevents a stale request from overwriting a later claim change. This operation does not authorize household administrators or other members to recover another creator's claim.

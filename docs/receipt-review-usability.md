# Receipt review usability

Receipt review uses compact item summaries with explicit Edit controls. Invalid amounts and missing descriptions stay expanded. Edit all remains available. The summary retains merchant, date, account, privacy and receipt total; the fixed footer shows the item total and reconciliation state alongside Save. Desktop comparison uses an optional wider dialog with a sticky photo pane; mobile retains the photo viewer in the footer.

Discount calculations retain printed gross values and show their suggested net amount separately from subsequent corrections. When automatic matching cannot reconcile a receipt, the reviewer can assign every observed discount to an item. A proposal is applied atomically only if all targets exist, all resulting amounts are positive, and the exact decimal sum equals the reviewed receipt total. Reapplication is blocked. Already reconciled receipts do not offer discount application. Unknown/invalid discounts stay manual corrections, never inferred charges or negative expense rows.

Apply suggested categories fills uncategorized items only, using the currently authorized, active catalogue. It does not overwrite chosen categories or opt into household learning. Remembering a category is an optional disclosure in each expanded item. Applying suggestions or discounts invalidates prior confirmation.

The printed total remains visible; correcting it is a separate disclosure explaining when that action is appropriate. Notes/privacy, labels and other optional controls remain available. Existing API validation, privacy, idempotency and confirmation requirements are unchanged. Existing drafts work without another scan or quota charge.

Queued and processing states have distinct copy. A pending receipt older than two minutes gets a longer-wait explanation on inbox refresh. This describes elapsed time, not a diagnosis of worker failure.

Verification includes exact discount assignment tests and EN/DE/HU mobile/desktop browser scenarios for compact rows, editing, zoom, confirmation, manual discount recovery and optional category learning. Test with new users before considering the experience usability-validated.

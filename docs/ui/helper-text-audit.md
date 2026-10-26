# Helper text audit

Reviewed against develop on 2026-10-25 and the canonical application HTML concept.

## Rule

Move optional explanations to `InfoPopover` beside the subject. Keep required input formats, validation, current state, empty-state actions, privacy decisions and financial consequences visible. Do not hide user-entered descriptions, account names, date ranges or currencies.

## Page-by-page decisions

| Page or flow | Information popover | Kept visible |
| --- | --- | --- |
| Dashboard | Page introduction, balance calculation, cash-flow scope, monthly summary, latest-transaction explanation, account balance explanation, allowance definition, category calculation and reporting details | Greeting/date, actual amounts/currencies, reporting period, setup prompt, loading/errors, empty states and planned-feature status |
| Accounts overview | Page introduction; tracked-balance explanation beside each balance label | Account scope/visibility summary, balance date, status, archive/restore consequences |
| Account create/edit | Basics-section introduction; existing locked-field explanations now share InfoPopover via AccountFieldHelp | Balance setup instructions, ownership/holder guidance, private/shared consequences, fixed badges, missing holders, unsaved changes and validation |
| Transactions overview | Page introduction; label-filter explanation | Active filters, loaded-record limitation, transaction data and empty/error states |
| Transaction create/edit | Basics, labels and receipt-item section explanations | Account currency, required input guidance, private visibility, recurring behavior, allowance links, allocation consequences and destructive confirmations |
| Transfers overview | Page introduction | Filter limitations, empty-state actions, redacted-side explanations |
| Transfer create/edit | No additional hiding | FX amounts, balance effects, private funding/return consequences, privacy, conflicts, archived accounts and void confirmation |
| Activities overview/chart | Purpose and total-calculation rules combined beside the page title; chart scope beside chart label | Currency and actual dates, privacy, date semantics in the editor, closed-state guidance, assignment replacement and empty states |
| Categories overview | Purpose; pin-order explanation | Read-only system category guidance, filters, hierarchy, empty states |
| Category editor | No additional hiding | Eligible parent restrictions, fixed type, historical assignments, shared-name privacy warning, archive/restore consequences |
| Statistics and category reports | Page introduction, yearly category explanation, category-report calculation details beside headings | Selected reporting period, currency, amounts, filters, legends and no-data messages |
| Recurring transactions | Page introduction | Automatic/manual posting behavior, date rules, privacy, blocked/unavailable status and confirm/skip/stop consequences |
| Household profile/members/privacy | Household introduction and profile-section explanation | Member-role vs financial-access distinction, currency conversion limitation, two-letter country format, permissions, navigation descriptions and privacy section |
| Household invitations/joining/history | No additional hiding | Email instructions, cutover/account-copy choices, acceptance/decline/revoke consequences and historical read-only scope |
| Personal Money | Overall allowance definition, monthly-schedule introduction and boundary-report explanation | Owner/privacy guidance, wallet privacy, grant/charge/refund/surrender/unlink consequences, reconciliation and history. The monthly create form explicitly retains the no-bank-transfer/carryover instruction |
| Saved views | Label-filter explanation only | Save/update/delete instructions and private saved-view scope |
| Onboarding and authentication | No changes | First-use instructions, required formats, password requirements, verification/recovery steps and errors |
| Public landing/contact | No changes | Product explanation, privacy messaging and contact instructions are primary content |
| Goals and user profile | No standalone route exists in this revision | Existing planned-feature placeholders retained; no new screens invented |

## Shared interaction

`shared/components/ui/InfoPopover.tsx` uses a native non-modal popover. The repository's existing popup primitive is `AverioDialog`, a modal with a mobile bottom sheet. It remains unchanged for forms and selections; a modal is not suitable for every short note.

The popover uses the same Tailwind colors, rounded border and emerald accent. Native top-layer rendering avoids clipping by cards or editor scroll containers and works inside AverioDialog. Placement follows the trigger and clamps to the visible viewport, flipping above when needed; long text scrolls. No dependency was added.

- Click, tap, Enter or Space toggles help; a second help control replaces the first.
- Outside click dismisses. Escape dismisses help and returns focus without closing the parent editor.
- The trigger is a 44px target with translated “About …” labeling, expanded state and dialog relationship.
- Help receives focus and an accessible description; it never submits its enclosing form.
- Existing explanatory translations are reused; the shared accessible-label template is translated in English, German and Hungarian.
- Do not put InfoPopover inside another button, link, summary or implicit form label. Place it beside the associated label or in a heading/legend.

## Verification

Run from `apps/averio-web`:

- `npm run build`
- `npm run lint`
- `node --test tests/*.test.mjs`
- `node tests/info-popover.browser.mjs` (optionally pass a Chrome executable path as the first argument)
- `node tests/transaction-editor.browser.mjs`
- `node tests/helper-pages.browser.mjs` (optionally pass a Chrome executable path)

The new browser suite covers all three languages, light/dark mode, 320px and desktop viewports, keyboard/touch, outside dismissal, only one open help control, nested-dialog Escape isolation, accidental submission, long-text viewport containment and language switching. Screenshots are written to `test-results/info-popover`.

# Refund and reimbursement terminology

Use different product names for the two money-back workflows. Keep English labels and internal/API identifiers unchanged.

| Workflow | English | German | Hungarian |
| --- | --- | --- | --- |
| Money returned against an original expense item | Refund / Item refunds | Rückzahlung / Rückzahlungen | Pénzvisszatérítés / Pénzvisszatérítések |
| A claim for someone to cover an expense | Reimbursement / Reimbursements | Kostenerstattung / Kostenerstattungen | Költségtérítés / Költségtérítések |

A **refund** records returned money, for example after a return, cancellation or price correction. It belongs to a receipt item and reduces its net expense/category cost. It is not ordinary income. This term also covers services and fees, so the UI does not restrict it to a purchase-price-only name.

A **reimbursement** tracks a claim for an employer, insurer, fund or another party to cover some or all of an expense. The expense can remain valid while that party covers its cost. The current claim workflow requires external submission confirmation and never records money as received automatically.

Apply the chosen terms consistently in headings, buttons, filters, badges, recurring creation choices, I-component help, Personal Money links and localized validation. Do not use the generic headings **Erstattungen** or **Visszatérítések** for both workflows. Ordinary contextual verbs may remain natural rather than repeating a compound noun in every sentence. Explain the distinction through `InfoPopover`, following the shared helper-text rule.

Examples:

- German: **Rückzahlung hinzufügen** versus **Kostenerstattung verfolgen**.
- Hungarian: **Pénzvisszatérítés hozzáadása** versus **Költségtérítés követése**.

Terminology references: [Verbraucherzentrale: returned purchase payments](https://www.verbraucherzentrale.de/media/20885/download), [German federal portal: Kostenerstattung](https://verwaltung.bund.de/leistungsverzeichnis/de/leistung/99134046017000), [NKFH: pénzvisszatérítés](https://nkfh.gov.hu/hirek/karacsony-utan-ha-nem-tetszik-az-ajandek-vagy-kettot-kaptunk-belole), [NAV: költségtérítés](https://nav.gov.hu/ado/szja/emelkedett-a-munkaba-jaras-koltsegteritese). These inform the product wording; they do not change the financial model or imply eligibility for a particular claim.

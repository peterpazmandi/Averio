const common = { status: 'Status', unknownProvider: 'Unknown provider', loading: 'Loading money back…', error: 'Money back could not be loaded.', empty: 'Nothing to track yet', emptyHelp: 'Refunds and reimbursement claims will appear here.', viewSource: 'View original payment', previous: 'Previous', next: 'Next', filters: { all: 'All', NeedsSubmission: 'Needs submission', Submitted: 'Submitted', PartiallyReceived: 'Partially received', Received: 'Received', Rejected: 'Rejected', Cancelled: 'Cancelled' }, statuses: { Received: 'Received', Cancelled: 'Cancelled', NeedsSubmission: 'Needs submission', Submitted: 'Submitted', PartiallyReceived: 'Partially received', Rejected: 'Rejected' } };
export const moneyBackEN = { title: 'Money back', intro: 'Track merchant refunds separately from reimbursement claims.', refund: 'Merchant refund', reimbursement: 'Reimbursement claim', ...common };
export const moneyBackDE = {
    title: 'Geld zurück',
    intro: 'Händlerrückzahlungen getrennt von Kostenerstattungsansprüchen verfolgen.',
    refund: 'Händlerrückzahlung',
    reimbursement: 'Kostenerstattungsanspruch',
    status: 'Status',
    unknownProvider: 'Unbekannter Anbieter',
    loading: 'Geld zurück wird geladen…',
    error: 'Geld zurück konnte nicht geladen werden.',
    empty: 'Noch nichts zu verfolgen',
    emptyHelp: 'Hier erscheinen Rückzahlungen und Kostenerstattungsansprüche.',
    viewSource: 'Ursprüngliche Zahlung ansehen',
    previous: 'Zurück',
    next: 'Weiter',
    filters: {
        all: 'Alle',
        NeedsSubmission: 'Einreichung erforderlich',
        Submitted: 'Eingereicht',
        PartiallyReceived: 'Teilweise eingegangen',
        Received: 'Eingegangen',
        Rejected: 'Abgelehnt',
        Cancelled: 'Storniert'
    },
    statuses: {
        Received: 'Eingegangen',
        Cancelled: 'Storniert',
        NeedsSubmission: 'Einreichung erforderlich',
        Submitted: 'Eingereicht',
        PartiallyReceived: 'Teilweise eingegangen',
        Rejected: 'Abgelehnt'
    }
};
export const moneyBackHU = {
    title: 'Visszakapott pénz',
    intro: 'Kövesd külön a kereskedői visszatérítéseket és a költségtérítési igényeket.',
    refund: 'Kereskedői visszatérítés',
    reimbursement: 'Költségtérítési igény',
    status: 'Állapot',
    unknownProvider: 'Ismeretlen szolgáltató',
    loading: 'A visszakapott pénz betöltése…',
    error: 'A visszakapott pénz nem tölthető be.',
    empty: 'Még nincs mit követni',
    emptyHelp: 'Itt jelennek meg a visszatérítések és a költségtérítési igények.',
    viewSource: 'Eredeti fizetés megtekintése',
    previous: 'Előző',
    next: 'Következő',
    filters: {
        all: 'Összes',
        NeedsSubmission: 'Beadásra vár',
        Submitted: 'Beadva',
        PartiallyReceived: 'Részben megérkezett',
        Received: 'Megérkezett',
        Rejected: 'Elutasítva',
        Cancelled: 'Megszakítva'
    },
    statuses: {
        Received: 'Megérkezett',
        Cancelled: 'Megszakítva',
        NeedsSubmission: 'Beadásra vár',
        Submitted: 'Beadva',
        PartiallyReceived: 'Részben megérkezett',
        Rejected: 'Elutasítva'
    }
};

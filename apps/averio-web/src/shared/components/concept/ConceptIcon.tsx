const paths = {
    dashboard: 'M4 13h6V4H4v9Zm0 7h6v-4H4v4Zm10 0h6v-9h-6v9Zm0-13h6V4h-6v3Z',
    accounts: 'M5 6h14a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2ZM3 10h18M7 15h4',
    transactions: 'M4 7h14M4 12h10M4 17h7m6-3 3 3-3 3',
    recurring: 'M20 7h-5V2M4 17h5v5M5.5 9A8 8 0 0 1 19 6l1 1M4 17l1 1a8 8 0 0 0 13.5-3',
    categories: 'M4 4h6v6H4zM14 4h6v6h-6zM4 14h6v6H4zM17 14v6M14 17h6',
    goals: 'M20 12a8 8 0 1 1-16 0 8 8 0 0 1 16 0Zm-5 0a3 3 0 1 1-6 0 3 3 0 0 1 6 0Zm2.5-5.5 2-2',
    statistics: 'M5 20V10M12 20V4M19 20v-7',
    household: 'M3 11 12 4l9 7M5 10v10h14V10M9 20v-6h6v6',
    profile: 'M16 8a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM4 21a8 8 0 0 1 16 0',
    menu: 'M4 7h16M4 12h16M4 17h16',
    close: 'm6 6 12 12M6 18 18 6',
    plus: 'M12 5v14M5 12h14',
    theme: 'M16 12a4 4 0 1 1-8 0 4 4 0 0 1 8 0ZM12 2v2M12 20v2M2 12h2M20 12h2M5 5l1 1m12 12 1 1M19 5l-1 1M6 18l-1 1',
    check: 'm3 12 4 4L21 4M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11',
    receipt: 'M6 3h12v18l-3-2-3 2-3-2-3 2V3ZM9 7h6M9 11h6M9 15h4',
};
export type ConceptIconName = keyof typeof paths;

export function ConceptIcon({ name }: { name: ConceptIconName }) {
    return (
        <svg className="av-icon" viewBox="0 0 24 24" fill="none"
            stroke="currentColor" strokeLinecap="round" strokeLinejoin="round"
            aria-hidden="true" focusable="false">
            <path d={paths[name]} />
        </svg>
    );
}

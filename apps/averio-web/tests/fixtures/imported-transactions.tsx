import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import '../../src/shared/styles/concept.css';
import { TransactionsPage } from '../../src/features/transactions/components/TransactionsPage';
import type { TransactionSearch } from '../../src/features/transactions/transactionSearch';

export function Fixture() {
    const [state, setState] = useState<TransactionSearch>({ period: 'all' });
    return <div className="av-ui"><TransactionsPage userId="user" householdId="home" urlState={state}
        onUrlChange={async patch => { setState(current => ({ ...current, ...patch })); }} /></div>;
}
const router = createRouter({ routeTree: createRootRoute({ component: Fixture }) });
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <RouterProvider router={router} />
</QueryClientProvider>);

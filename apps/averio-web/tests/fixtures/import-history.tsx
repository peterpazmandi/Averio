import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import { ImportHistoryPage } from '../../src/features/imports/ImportHistoryPage';
export function Fixture() {
    const [batch, setBatch] = useState<string>();
    return <ImportHistoryPage key={batch ?? 'list'} userId="user" householdId="home" batchId={batch} onSelect={setBatch} />;
}
const route = createRootRoute({ component: Fixture });
const router = createRouter({ routeTree: route });
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <RouterProvider router={router} />
</QueryClientProvider>);

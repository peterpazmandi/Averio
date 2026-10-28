import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import { ReceiptsPage } from '../../src/features/receipts/ReceiptsPage';

const route = createRootRoute({ component: () => <ReceiptsPage userId="user" householdId="home" /> });
const router = createRouter({ routeTree: route });
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <RouterProvider router={router} />
</QueryClientProvider>);

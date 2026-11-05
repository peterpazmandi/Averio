import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRootRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import { ImportReconciliation, ImportUndo } from '../../src/features/imports/ImportTools';
export function Fixture() { return <main className="mx-auto grid max-w-3xl gap-6 p-4"><ImportReconciliation userId="user" householdId="home" batchId="batch" /><ImportUndo userId="user" householdId="home" batchId="batch" /></main>; }
const router = createRouter({ routeTree: createRootRoute({ component: Fixture }) });
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}><RouterProvider router={router} /></QueryClientProvider>);
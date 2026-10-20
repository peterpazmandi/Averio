import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { createRootRoute, createRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import '../../src/shared/styles/concept.css';
import { HouseholdPage } from '../../src/features/household/HouseholdPage';

// Include the production router's scroll restoration, not only the page layout.
const rootRoute = createRootRoute({ component: () =>
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <div className="av-ui"><div className="av-shell">
            <header className="av-topbar">Averio</header>
            <main className="av-content"><HouseholdPage userId="user" householdId="home" /></main>
        </div>
        </div>
    </QueryClientProvider>,
});
const routeTree = rootRoute.addChildren([
    createRoute({ getParentRoute: () => rootRoute, path: '/tests/fixtures/personal-money.html' }),
]);
const router = createRouter({ routeTree, scrollRestoration: true });
createRoot(document.getElementById('root')!).render(<RouterProvider router={router} />);

import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider, useQueryClient } from '@tanstack/react-query';
import { createRootRoute, createRoute, createRouter, RouterProvider } from '@tanstack/react-router';
import '../../src/i18n';
import '../../src/index.css';
import '../../src/shared/styles/concept.css';
import { HouseholdPage } from '../../src/features/household/HouseholdPage';

import { PersonalMoneyCard } from '../../src/features/dashboard/components/PersonalMoneyCard';
import { personalMoneyKeys } from '../../src/features/personal-money/personalMoneyQueries';

function DashboardFixture() {
    const client = useQueryClient();
    return <>
        <PersonalMoneyCard userId="user" householdId="home" />
        <button onClick={() => void client.invalidateQueries({ queryKey: personalMoneyKeys.mine('user', 'home') })}>
            Simulate wallet mutation
        </button>
    </>;
}

// Include the production router's scroll restoration, not only the page layout.
const rootRoute = createRootRoute({ component: () =>
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <div className="av-ui"><div className="av-shell">
            <header className="av-topbar">Averio</header>
            <main className="av-content">{new URLSearchParams(location.search).has('dashboard')
                ? <DashboardFixture /> : <HouseholdPage userId="user" householdId="home" />}</main>
        </div>
        </div>
    </QueryClientProvider>,
});
const routeTree = rootRoute.addChildren([
    createRoute({ getParentRoute: () => rootRoute, path: '/tests/fixtures/personal-money.html' }),
]);
const router = createRouter({ routeTree, scrollRestoration: true });
createRoot(document.getElementById('root')!).render(<RouterProvider router={router} />);

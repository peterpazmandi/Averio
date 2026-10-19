import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '../../src/i18n';
import '../../src/index.css';
import '../../src/shared/styles/concept.css';
import { HouseholdPage } from '../../src/features/household/HouseholdPage';

// Exercise the real page and shell header CSS with deterministic HTTP fixtures.
createRoot(document.getElementById('root')!).render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
        <div className="av-ui"><header className="av-topbar">Averio</header>
            <main className="p-4"><HouseholdPage userId="user" householdId="home" /></main>
        </div>
    </QueryClientProvider>,
);

import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '../../src/i18n';
import '../../src/index.css';
import { TransactionEditor } from '../../src/features/transactions/components/TransactionEditor';
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <TransactionEditor userId="user" householdId="home" transaction={null} busy={false} onCancel={() => {}}
        onSave={async request => { document.body.dataset.saved = JSON.stringify(request); }} onSaveRecurring={async () => {}} />
</QueryClientProvider>);

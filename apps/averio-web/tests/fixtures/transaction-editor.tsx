import { createRoot } from 'react-dom/client';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '../../src/i18n';
import '../../src/index.css';
import { TransactionEditor } from '../../src/features/transactions/components/TransactionEditor';
import type { TransactionDto } from '../../src/features/transactions/api/transactionApi';
declare global { interface Window { editorFixture?: TransactionDto } }
createRoot(document.getElementById('root')!).render(<QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
    <TransactionEditor userId="user" householdId="home" transaction={window.editorFixture ?? null} busy={false} onCancel={() => {}}
        onArchive={async (archived, version) => { document.body.dataset.archived = JSON.stringify({ archived, version }); }}
        onVoid={async version => { document.body.dataset.voided = version; }}
        onSave={async request => { document.body.dataset.saved = JSON.stringify(request); }} onSaveRecurring={async () => {}} />
</QueryClientProvider>);

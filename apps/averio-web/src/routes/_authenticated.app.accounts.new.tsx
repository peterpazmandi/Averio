import { createFileRoute } from '@tanstack/react-router';
import { AccountEditorPage } from '../features/accounts/components/AccountEditorPage';

export const Route = createFileRoute('/_authenticated/app/accounts/new')({
    component: () => <AccountEditorPage />,
});

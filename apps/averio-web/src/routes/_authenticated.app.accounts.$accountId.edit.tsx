import { createFileRoute } from '@tanstack/react-router';
import { AccountEditorPage } from '../features/accounts/components/AccountEditorPage';

export const Route = createFileRoute('/_authenticated/app/accounts/$accountId/edit')({
    component: function EditAccountRoute() {
        const { accountId } = Route.useParams();
        return <AccountEditorPage accountId={accountId} />;
    },
});

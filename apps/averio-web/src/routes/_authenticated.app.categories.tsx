import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { CategoriesPage } from '../features/categories/CategoriesPage';

export const Route = createFileRoute('/_authenticated/app/categories')({ component: CategoriesRoute });

function CategoriesRoute() {
    const auth = useAuth();
    if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null;
    return <CategoriesPage userId={auth.user.userId} householdId={auth.user.household.householdId} />;
}

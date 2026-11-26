import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { MoneyBackPage } from '../features/money-back/MoneyBackPage';
export const Route = createFileRoute('/_authenticated/app/money-back')({ component: MoneyBackRoute });
function MoneyBackRoute() { const auth = useAuth(); if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null; return <MoneyBackPage key={auth.user.userId + auth.user.household.householdId} userId={auth.user.userId} householdId={auth.user.household.householdId} />; }

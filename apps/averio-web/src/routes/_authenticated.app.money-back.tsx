import { createFileRoute } from '@tanstack/react-router';
import { useAuth } from '../features/authentication/context/AuthProvider';
import { MoneyBackPage } from '../features/money-back/MoneyBackPage';
export const Route = createFileRoute('/_authenticated/app/money-back')({ component: MoneyBackRoute });
function MoneyBackRoute() { const auth = useAuth(); if (auth.status !== 'authenticated' || !auth.user || auth.user.onboardingRequired) return null; return <MoneyBackPage householdId={auth.user.household.householdId} />; }

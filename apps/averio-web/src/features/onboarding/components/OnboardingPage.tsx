import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { useQuery } from '@tanstack/react-query';
import { invitationApi, invitationKeys } from '../../household/invitationQueries';
import { InvitationPage } from '../../household/InvitationPage';
import { PrimaryButton } from '../../../shared/components/PrimaryButton';
import { useAuth } from '../../authentication/context/AuthProvider';
import { OnboardingForUser } from './OnboardingForUser';

export type Currency = 'HUF' | 'EUR' | 'USD';

/**
 * Selects the authenticated user.
 *
 * Changing the key remounts the entire onboarding subtree,
 * including its form state.
 */
export function OnboardingPage() {
    const auth = useAuth();
    const navigate = useNavigate();
    const { t } = useTranslation();
    const pending = useQuery({ queryKey: invitationKeys.pending(auth.user?.userId),
        queryFn: ({ signal }) => invitationApi.apiV1InvitationsPendingGet({ signal }),
        enabled: auth.status === 'authenticated' });

    if (auth.status === 'loading') {
        return (
            <main
                className="
                    min-h-screen
                    bg-slate-50
                    px-5 py-8
                    text-slate-950
                    dark:bg-slate-950
                    dark:text-white
                "
            >
                <p role="status">
                    {t('common.status.loading')}
                </p>
            </main>
        );
    }

    if (auth.status !== 'authenticated' || !auth.user) {
        return (
            <main
                className="
                    min-h-screen
                    bg-slate-50
                    px-5 py-8
                    text-slate-950
                    dark:bg-slate-950
                    dark:text-white
                "
            >
                <div className="mx-auto max-w-md space-y-4">
                    <p role="alert">
                        {auth.status === 'anonymous'
                            ? t('auth.session.expiredDescription')
                            : t('errors.genericDescription')}
                    </p>

                    <PrimaryButton
                        type="button"
                        onClick={() => {
                            void navigate({
                                to: '/login',
                                search: {
                                    redirect: undefined,
                                },
                            });
                        }}
                    >
                        {t('auth.forgot.backToLogin')}
                    </PrimaryButton>
                </div>
            </main>
        );
    }

    if (pending.isPending) return <p role="status">{t('common.status.loading')}</p>;
    if (pending.isError) return <div role="alert"><p>{t('invitations.loadError')}</p><PrimaryButton onClick={() => void pending.refetch()}>{t('common.actions.retry')}</PrimaryButton></div>;
    if (pending.data.length > 0) return <InvitationPage />;

    return (
        <OnboardingForUser
            key={auth.user.userId}
            userId={auth.user.userId}
        />
    );
}



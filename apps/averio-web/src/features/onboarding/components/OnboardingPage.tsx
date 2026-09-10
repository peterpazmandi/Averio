import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
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

    return (
        <OnboardingForUser
            key={auth.user.userId}
            userId={auth.user.userId}
        />
    );
}


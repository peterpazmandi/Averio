import { useEffect, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { PrimaryButton } from '../../../shared/components/PrimaryButton';
import { accountsQueryKeys } from '../../accounts/api/accountsQueryKeys';
import { useAuth } from '../../authentication/context/AuthProvider';
import { dashboardQueryKeys } from '../../dashboard/api/dashboardQueryKeys';

export function OnboardingCompleted({
    userId,
}: {
    userId: string;
}) {
    const { refresh } = useAuth();
    const queryClient = useQueryClient();
    const navigate = useNavigate();
    const { t } = useTranslation();

    const [attempt, setAttempt] = useState(0);
    const [hasError, setHasError] = useState(false);

    useEffect(() => {
        let cancelled = false;

        async function continueToApp(): Promise<void> {
            try {
                const user = await refresh();

                if (cancelled) {
                    return;
                }

                if (
                    user.userId !== userId
                    || user.onboardingRequired
                    || !user.household
                ) {
                    setHasError(true);
                    return;
                }

                const householdId = user.household.householdId;

                await Promise.all([
                    queryClient.invalidateQueries({
                        queryKey: accountsQueryKeys.household(
                            userId,
                            householdId,
                        ),
                        refetchType: 'none',
                    }),
                    queryClient.invalidateQueries({
                        queryKey: dashboardQueryKeys.household(
                            userId,
                            householdId,
                        ),
                        refetchType: 'none',
                    }),
                ]);

                if (cancelled) {
                    return;
                }

                await navigate({
                    to: '/app',
                    replace: true,
                });
            }
            catch {
                if (!cancelled) {
                    setHasError(true);
                }
            }
        }

        void continueToApp();

        return () => {
            cancelled = true;
        };
    }, [
        attempt,
        navigate,
        queryClient,
        refresh,
        userId,
    ]);

    return (
        <div className="space-y-4">
            <p role="status">
                {t('onboarding.setupCompleted')}
            </p>

            {hasError ? (
                <>
                    <p
                        role="alert"
                        className="
                            text-sm
                            text-red-700
                            dark:text-red-400
                        "
                    >
                        {t('errors.genericDescription')}
                    </p>

                    <PrimaryButton
                        type="button"
                        onClick={() => {
                            setHasError(false);
                            setAttempt((value) => value + 1);
                        }}
                    >
                        {t('common.actions.retry')}
                    </PrimaryButton>
                </>
            ) : (
                <p role="status">
                    {t('common.status.loading')}
                </p>
            )}
        </div>
    );
}
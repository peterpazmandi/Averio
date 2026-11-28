import { useEffect, useRef, useState } from 'react';
import type { PropsWithChildren } from 'react';
import { useMutation, useQuery } from '@tanstack/react-query';
import { useNavigate, useRouterState } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { AppNavigation } from './AppNavigation';
import { logoutUser } from '../../features/authentication/api/authApi';
import { useAuth } from '../../features/authentication/context/AuthProvider';
import { onboardingQueries } from '../../features/onboarding/api/onboardingQueries';
import { AverioBrand } from './branding/AverioBrand';
import { ConceptIcon } from './concept/ConceptIcon';
import { ConceptPreferences } from './concept/ConceptPreferences';
import { AverioButton } from './ui/AverioButton';
import { AverioDialog } from './ui/AverioDialog';
import { InfoPopover } from './ui/InfoPopover';
import '../styles/concept.css';
import { ProfileAvatar } from '../../features/profile/ProfileAvatar';
import { notificationsApi, notificationKeys } from '../../features/notifications/notificationsApi';

export function AppShell({ children }: PropsWithChildren) {
    const auth = useAuth();
    const navigate = useNavigate();
    const { t, i18n } = useTranslation();
    const pathname = useRouterState({ select: (state) => state.location.pathname });
    const dialogRef = useRef<HTMLDialogElement>(null);
    const [menuOpen, setMenuOpen] = useState(false);
    const [preferencesOpen, setPreferencesOpen] = useState(false);
    const [preferencesClosing, setPreferencesClosing] = useState(false);
    const userId = auth.user?.userId ?? '';
    const statusQuery = useQuery({
        ...onboardingQueries.status(userId),
        enabled: auth.status === 'authenticated' && !!userId,
    });
    const currentHouseholdId = auth.user?.household.householdId ?? '';
    const notificationCount = useQuery({
        queryKey: notificationKeys.count(userId, currentHouseholdId), queryFn: () => notificationsApi.count(currentHouseholdId),
        enabled: auth.status === 'authenticated' && !!userId && !!currentHouseholdId,
        refetchInterval: 60_000, refetchOnWindowFocus: true,
    });

    const logoutMutation = useMutation({
        mutationFn: logoutUser,
        onSuccess: async () => {
            await auth.clear();
            await navigate({ to: '/login', search: { redirect: undefined } });
        },
    });

    useEffect(() => {
        const dialog = dialogRef.current;
        if (!dialog || !menuOpen) return;
        dialog.showModal();
        const previous = document.body.style.overflow;
        document.body.style.overflow = 'hidden';
        return () => {
            dialog.close();
            document.body.style.overflow = previous;
        };
    }, [menuOpen]);

    useEffect(() => {
        if (!preferencesClosing) return;
        // A transition can be interrupted by a viewport or motion-preference change.
        const delay = window.matchMedia('(prefers-reduced-motion: reduce)').matches ? 0 : 400;
        const timeout = window.setTimeout(() => {
            setPreferencesOpen(false);
            setPreferencesClosing(false);
        }, delay);
        return () => window.clearTimeout(timeout);
    }, [preferencesClosing]);

    if (auth.status !== 'authenticated' || !auth.user) return null;

    const user = auth.user;
    const displayName = user.displayName || user.email;
    const initials = displayName.trim().split(/\s+/).slice(0, 2)
        .map((part) => part[0]).join('').toLocaleUpperCase();
    const household = statusQuery.data?.household;
    const householdName = household?.name || t('concept.household');
    const isTransactionsPage = pathname.replace(/\/+$/, '') === '/app/transactions';
    const title = pathname.startsWith('/app/notifications') ? t('inbox.title') : pathname.startsWith('/app/profile') ? t('profile.title') : pathname.startsWith('/app/receipts') ? t('receipts.title') : pathname.startsWith('/app/recurring') ? t('recurring.title') : pathname.startsWith('/app/household')
        ? t('householdSettings.title')
        : pathname.startsWith('/app/accounts')
        ? t('app.navigation.accounts')
        : pathname.startsWith('/app/transfers')
            ? t('transfers.title')
        : pathname.startsWith('/app/activities')
            ? t('activities.title')
        : pathname.startsWith('/app/transactions')
            ? t('navigation.transactions')
            : pathname.startsWith('/app/statistics')
                ? t('navigation.statistics')
                : t('app.navigation.dashboard');
    const month = new Intl.DateTimeFormat(i18n.resolvedLanguage ?? i18n.language, {
        month: 'long', year: 'numeric',
    }).format(new Date());

    function sidebarContent() {
        return (
            <>
                <div className="av-brand"><AverioBrand variant="on-dark" /></div>
                <div className="av-workspace">
                    <div className="av-workspace-label">{t('concept.currentHousehold')}</div>
                    <div className="av-workspace-main">
                        <span className="av-workspace-avatar"><ConceptIcon name="household" /></span>
                        <span>{householdName}</span>
                    </div>
                    {household?.preferredCurrency && (
                        <div className="av-workspace-sub">
                            {t('concept.reportingCurrency', { currency: household.preferredCurrency })}
                        </div>
                    )}
                </div>
                <AppNavigation onNavigate={() => setMenuOpen(false)} />
                <div className="av-sidebar-bottom">
                    <div className="av-help-card">
                        <strong>{t('concept.receiptTitle')}</strong>
                        <p>{t('concept.receiptDescription')}</p>
                        <button type="button" className="min-h-11 rounded-lg px-2 text-sm font-semibold text-emerald-300 focus-visible:outline-2" onClick={() => { setMenuOpen(false); void navigate({ to: '/app/receipts' }); }}>{t('receipts.title')}</button>
                    </div>
                </div>
            </>
        );
    }

    return (
        <div className="av-ui">
            <a href="#main-content" className="av-skip">{t('concept.skip')}</a>
            <aside className="av-sidebar">{sidebarContent()}</aside>
            <dialog ref={dialogRef} className="av-mobile-dialog"
                aria-label={t('app.navigation.label')}
                onCancel={() => setMenuOpen(false)}
                onClose={() => setMenuOpen(false)}
                onClick={(event) => {
                    if (event.target === event.currentTarget) setMenuOpen(false);
                }}>
                <button type="button" className="av-icon-btn av-close-menu"
                    aria-label={t('concept.closeMenu')} onClick={() => setMenuOpen(false)}>
                    <ConceptIcon name="close" />
                </button>
                {sidebarContent()}
            </dialog>

            <div className="av-shell">
                <header className="av-topbar max-[360px]:gap-0! max-[360px]:px-1!">
                    <button type="button" className="av-icon-btn av-mobile-menu"
                        aria-label={t('concept.openMenu')} aria-expanded={menuOpen}
                        onClick={() => setMenuOpen(true)}>
                        <ConceptIcon name="menu" />
                    </button>
                    <div className="av-topbar-title">
                        <div className="av-eyebrow">{householdName} · {month}</div>
                        <div className="flex min-w-0 items-center">
                            <h1>{title}</h1>
                            {isTransactionsPage && <InfoPopover label={t('transactionManagement.title')}>{t('transactionManagement.description')}</InfoPopover>}
                        </div>
                    </div>
                    <div className="av-top-actions max-[360px]:gap-1!">
                        {!isTransactionsPage && <button type="button" className="av-btn av-add-button"
                            onClick={() => {
                                void navigate({
                                    to: '/app/transactions',
                                    search: { create: true },
                                });
                            }}>
                            <ConceptIcon name="plus" />{t('concept.addTransaction')}
                        </button>}
                        <div className="max-[601px]:hidden"><ConceptPreferences /></div>
                        <AverioButton size="icon" className="relative" aria-label={t('inbox.bell', { count: notificationCount.data?.count ?? 0 })}
                            onClick={() => void navigate({ to: '/app/notifications' })}>
                            <ConceptIcon name="bell" />
                            {!!notificationCount.data?.count && <span aria-hidden="true" className="absolute -right-1 -top-1 rounded-full bg-emerald-600 px-1.5 text-xs font-bold text-white">{notificationCount.data.count > 9 ? '9+' : notificationCount.data.count}</span>}
                        </AverioButton>
                        <div className="hidden max-[601px]:block">
                            <AverioButton size="icon" aria-label={t('preferences.title')} aria-haspopup="dialog"
                                aria-expanded={preferencesOpen} onClick={() => setPreferencesOpen(true)}>
                                <ConceptIcon name="more" />
                            </AverioButton>
                        </div>
                        <div className="av-profile">
                            <ProfileAvatar userId={userId} initials={initials} className="av-avatar" />
                            <div className="av-profile-copy">
                                <strong>{displayName}</strong>
                                <small>{t('concept.household')}</small>
                            </div>
                        </div>
                        <button type="button" className="av-btn max-[480px]:size-11 max-[480px]:p-0!"
                            aria-label={t(logoutMutation.isPending ? 'common.actions.signingOut' : 'common.actions.signOut')}
                            disabled={logoutMutation.isPending}
                            onClick={() => logoutMutation.mutate()}>
                            <span className="max-[480px]:hidden">{t(logoutMutation.isPending
                                ? 'common.actions.signingOut' : 'common.actions.signOut')}</span>
                            <span className="hidden max-[480px]:block"><ConceptIcon name="logout" /></span>
                        </button>
                    </div>
                </header>
                {preferencesOpen && <AverioDialog open title={t('preferences.title')} closeLabel={t('common.actions.close')}
                    dismissOnBackdrop onDismiss={() => setPreferencesClosing(true)}
                    onTransitionEnd={event => {
                        if (preferencesClosing && event.target === event.currentTarget && event.propertyName === 'translate') {
                            setPreferencesOpen(false);
                            setPreferencesClosing(false);
                        }
                    }}
                    className={`motion-safe:transition-[translate,opacity] motion-safe:duration-200 motion-safe:ease-out motion-safe:starting:translate-y-full motion-safe:starting:opacity-0 motion-safe:backdrop:transition-opacity motion-safe:backdrop:duration-200 motion-safe:starting:backdrop:opacity-0 ${preferencesClosing ? 'motion-safe:translate-y-full motion-safe:opacity-0 motion-safe:backdrop:opacity-0' : ''}`}>
                    <ConceptPreferences layout="sheet" />
                </AverioDialog>}
                <main id="main-content" tabIndex={-1} className="av-content">
                    {logoutMutation.isError && (
                        <p role="alert" className="av-error">{t('errors.genericDescription')}</p>
                    )}
                    {children}
                </main>
            </div>
        </div>
    );
}

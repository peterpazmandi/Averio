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
import '../styles/concept.css';

export function AppShell({ children }: PropsWithChildren) {
    const auth = useAuth();
    const navigate = useNavigate();
    const { t, i18n } = useTranslation();
    const pathname = useRouterState({ select: (state) => state.location.pathname });
    const dialogRef = useRef<HTMLDialogElement>(null);
    const [menuOpen, setMenuOpen] = useState(false);
    const userId = auth.user?.userId ?? '';
    const statusQuery = useQuery({
        ...onboardingQueries.status(userId),
        enabled: auth.status === 'authenticated' && !!userId,
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

    if (auth.status !== 'authenticated' || !auth.user) return null;

    const user = auth.user;
    const displayName = user.displayName || user.email;
    const initials = displayName.trim().split(/\s+/).slice(0, 2)
        .map((part) => part[0]).join('').toLocaleUpperCase();
    const household = statusQuery.data?.household;
    const householdName = household?.name || t('concept.household');
    const title = pathname.startsWith('/app/accounts')
        ? t('app.navigation.accounts')
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
                        <span className="av-nav-badge">{t('concept.soon')}</span>
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
                <header className="av-topbar">
                    <button type="button" className="av-icon-btn av-mobile-menu"
                        aria-label={t('concept.openMenu')} aria-expanded={menuOpen}
                        onClick={() => setMenuOpen(true)}>
                        <ConceptIcon name="menu" />
                    </button>
                    <div className="av-topbar-title">
                        <div className="av-eyebrow">{householdName} · {month}</div>
                        <h1>{title}</h1>
                    </div>
                    <div className="av-top-actions">
                        <button type="button" className="av-btn av-add-button"
                            onClick={() => {
                                void navigate({
                                    to: '/app/transactions',
                                    search: { create: true },
                                });
                            }}>
                            <ConceptIcon name="plus" />{t('concept.addTransaction')}
                        </button>
                        <ConceptPreferences />
                        <div className="av-profile">
                            <span className="av-avatar" aria-hidden="true">{initials}</span>
                            <div className="av-profile-copy">
                                <strong>{displayName}</strong>
                                <small>{t('concept.household')}</small>
                            </div>
                        </div>
                        <button type="button" className="av-btn"
                            disabled={logoutMutation.isPending}
                            onClick={() => logoutMutation.mutate()}>
                            {t(logoutMutation.isPending
                                ? 'common.actions.signingOut' : 'common.actions.signOut')}
                        </button>
                    </div>
                </header>
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

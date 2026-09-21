import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { ConceptIcon } from './concept/ConceptIcon';
import type { ConceptIconName } from './concept/ConceptIcon';

export function AppNavigation({ onNavigate }: { onNavigate?: () => void }) {
    const { t } = useTranslation();

    function unavailable(name: ConceptIconName) {
        return (
            <div key={name} className="av-nav-item" aria-disabled="true">
                <ConceptIcon name={name} />
                <span>{t('concept.nav.' + name)}</span>
                <span className="av-nav-badge">{t('concept.soon')}</span>
            </div>
        );
    }

    return (
        <nav className="av-nav" aria-label={t('app.navigation.label')}>
            <div className="av-nav-label">{t('concept.overview')}</div>
            <Link to="/app" activeOptions={{ exact: true }} className="av-nav-item"
                activeProps={{ className: 'active', 'aria-current': 'page' }}
                onClick={onNavigate}>
                <ConceptIcon name="dashboard" />{t('app.navigation.dashboard')}
            </Link>

            <div className="av-nav-label">{t('concept.money')}</div>
            <Link to="/app/accounts" className="av-nav-item"
                activeProps={{ className: 'active', 'aria-current': 'page' }}
                onClick={onNavigate}>
                <ConceptIcon name="accounts" />{t('app.navigation.accounts')}
            </Link>
            <Link to="/app/transactions" className="av-nav-item"
                activeProps={{ className: 'active', 'aria-current': 'page' }}
                onClick={onNavigate}>
                <ConceptIcon name="transactions" />{t('navigation.transactions')}
            </Link>
            {(['recurring', 'categories'] as const).map(unavailable)}

            <div className="av-nav-label">{t('concept.planning')}</div>
            {unavailable('goals')}
            <Link to="/app/statistics" className="av-nav-item"
                activeProps={{ className: 'active', 'aria-current': 'page' }}
                onClick={onNavigate}>
                <ConceptIcon name="statistics" />{t('navigation.statistics')}
            </Link>

            <div className="av-nav-label">{t('concept.settings')}</div>
            {(['household', 'profile'] as const).map(unavailable)}
        </nav>
    );
}

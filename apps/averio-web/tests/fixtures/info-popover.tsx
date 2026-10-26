/* eslint-disable react-refresh/only-export-components -- Standalone browser-test entry point. */
import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { useTranslation } from 'react-i18next';
import i18n from '../../src/i18n';
import '../../src/index.css';
import { InfoPopover } from '../../src/shared/components/ui/InfoPopover';
import { AverioDialog } from '../../src/shared/components/ui/AverioDialog';
import { AverioCard } from '../../src/shared/components/ui/AverioCard';
import { AccountFieldHelp } from '../../src/features/accounts/components/AccountFieldHelp';
import { CategoryReportHelp } from '../../src/features/dashboard/components/CategoryReportHelp';

function Fixture() {
    const { t } = useTranslation();
    const [dialog, setDialog] = useState(false);
    const [submitted, setSubmitted] = useState(0);
    return <main className="min-h-screen space-y-4 p-4">
        <select aria-label="Language" value={i18n.resolvedLanguage} onChange={e => void i18n.changeLanguage(e.target.value)}>
            {['en', 'de', 'hu'].map(lang => <option key={lang}>{lang}</option>)}
        </select>
        <AverioCard>
            <h2 className="text-xl font-semibold" aria-label={t('personalMoney.myWallet')}>{t('personalMoney.myWallet')}
                <InfoPopover label={t('personalMoney.myWallet')}>{t('personalMoney.description')}</InfoPopover>
            </h2>
            <h3 aria-label={t('categorySpending.title')}>{t('categorySpending.title')}<CategoryReportHelp kind="Expense" /></h3>
            <button type="button" onClick={() => setDialog(true)}>Open editor</button>
        </AverioCard>
        <output>{submitted}</output>
        <AverioCard>
            <label htmlFor="locked-currency">{t('onboarding.account.currency')}</label>
            <select id="locked-currency" disabled aria-describedby="locked-currency-help"><option>EUR</option></select>
            <AccountFieldHelp id="locked-currency-help" field={t('onboarding.account.currency')}>{t('accountManagement.currencyFixed')}</AccountFieldHelp>
            <label htmlFor="locked-balance">{t('app.accounts.recordedBalance')}</label>
            <input id="locked-balance" disabled value="100" aria-describedby="locked-balance-help" />
            <AccountFieldHelp id="locked-balance-help" field={t('app.accounts.recordedBalance')}>{t('accountManagement.balanceFixed')}</AccountFieldHelp>
        </AverioCard>
        <div className="flex min-h-[700px] items-end justify-end">
            <InfoPopover label="Edge">{Array.from({ length: 12 }, (_, i) => <span className="mb-2 block" key={i}>{t('personalMoney.description')}</span>)}</InfoPopover>
        </div>
        <AverioDialog open={dialog} title="Editor" closeLabel="Close editor" onDismiss={() => setDialog(false)}
            actions={<button type="submit" form="fixture-form">Save</button>}>
            <form id="fixture-form" onSubmit={e => { e.preventDefault(); setSubmitted(n => n + 1); }}>
                <h3 aria-label={t('transactionManagement.items')}>{t('transactionManagement.items')}
                    <InfoPopover label={t('transactionManagement.items')}>{t('transactionManagement.itemsHelp')}</InfoPopover>
                </h3>
                <input aria-label="Name" />
                <p>{t('transactionManagement.privateHelp')}</p>
            </form>
        </AverioDialog>
    </main>;
}
createRoot(document.getElementById('root')!).render(<Fixture />);

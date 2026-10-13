import type { UseQueryResult } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { AverioDialog } from '../../../shared/components/ui/AverioDialog';
import { AverioButton } from '../../../shared/components/ui/AverioButton';
import { formatDecimalAmount } from '../../../shared/money/decimalAmount';
import type { TransactionDto } from '../api/transactionApi';

export function TransactionLinkDetail({ query, onClose }: { query: UseQueryResult<TransactionDto, Error>; onClose: () => void }) {
    const { t, i18n } = useTranslation();
    const transaction = query.data;
    const money = (amount: string) => formatDecimalAmount(amount, transaction!.currency, i18n.resolvedLanguage ?? i18n.language);
    return <AverioDialog open title={t('transactionLinks.details')} closeLabel={t('common.actions.close')}
        onDismiss={onClose} actions={<AverioButton onClick={onClose}>{t('common.actions.close')}</AverioButton>}>
        {query.isPending ? <p role="status">{t('common.status.loading')}</p> : query.isError ? <div className="space-y-3">
            <p role="alert">{t('transactionLinks.unavailable')}</p>
            <AverioButton disabled={query.isFetching} onClick={() => { void query.refetch(); }}>{t('common.actions.retry')}</AverioButton>
        </div> : transaction && <div className="space-y-4">
            <p className="text-xs">{t('transactionLinks.readOnly')}</p>
            <h3 className="break-words text-base font-semibold">{transaction.description}</h3>
            <dl className="grid grid-cols-2 gap-3">
                <dt>{t('transactionManagement.account')}</dt><dd className="break-words">{transaction.accountName}</dd>
                <dt>{t('transactionManagement.date')}</dt><dd>{new Intl.DateTimeFormat(i18n.resolvedLanguage, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(transaction.occurredOn + 'T00:00:00Z'))}</dd>
                <dt>{t('transactionManagement.type')}</dt><dd>{t(transaction.kind === 'Income' ? 'transactionManagement.income' : 'transactionManagement.expense')}</dd>
                <dt>{t('transactionManagement.amount')}</dt><dd className="break-all font-semibold tabular-nums">{money(transaction.amount)}</dd>
            </dl>
            {transaction.status === 'Voided' && <p>{t('transactionManagement.voided')}</p>}
            {transaction.note && <p className="whitespace-pre-wrap break-words">{transaction.note}</p>}
            <ul className="space-y-3">{transaction.items.map(item => <li key={item.id} className="flex flex-wrap justify-between gap-2 border-t border-slate-200 pt-3 dark:border-slate-700">
                <span className="min-w-0 break-words">{item.description}<span className="block text-xs">{item.categoryName}</span></span>
                <span className="break-all tabular-nums">{money(item.amount)}</span>
            </li>)}</ul>
        </div>}
    </AverioDialog>;
}

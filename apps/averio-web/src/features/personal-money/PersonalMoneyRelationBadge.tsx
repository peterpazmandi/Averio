import { useTranslation } from 'react-i18next';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { formatDecimalAmount } from '../../shared/money/decimalAmount';
import type { PersonalMoneyRelation } from './personalMoneyRelations';

export function PersonalMoneyRelationBadge({ relation, detail = false }: {
    relation: PersonalMoneyRelation; detail?: boolean;
}) {
    const { t, i18n } = useTranslation();
    return <span className="inline-flex max-w-full flex-wrap items-center gap-1.5 rounded-lg border border-emerald-400/30 bg-emerald-100/80 px-2.5 py-1 text-[11px] font-semibold leading-snug text-emerald-900 dark:border-emerald-600/40 dark:bg-emerald-950/70 dark:text-emerald-100">
        <ConceptIcon name="savings" />
        <span>{t('personalMoney.relations.' + relation.kind)}</span>
        {detail && <span className="whitespace-nowrap tabular-nums">
            {formatDecimalAmount(relation.amount, relation.currency, i18n.resolvedLanguage ?? i18n.language)}
        </span>}
    </span>;
}

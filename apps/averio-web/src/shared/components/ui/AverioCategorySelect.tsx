import { useId, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { CategoryResponse } from '../../api/generated';
import { getCategoryDisplayName } from '../../../features/transactions/categoryLocalization';
import { normalizeSearch, sortCategories } from '../../../features/categories/categoryOrdering';
import { categoryPath, categoryTree, categoryIndent } from '../../../features/categories/categoryTree';
import { AverioButton } from './AverioButton';
import { AverioInput } from './AverioFormControls';
import { AverioDialog } from './AverioDialog';
import { AverioCard } from './AverioCard';

interface Props {
    categories: CategoryResponse[];
    contextCategories?: CategoryResponse[];
    value: string;
    onChange: (id: string) => void;
    disabled?: boolean;
    label: string;
    noneLabel?: string;
    fallback?: { categoryName: string; categoryIcon: string };
}

export function AverioCategorySelect({ categories, contextCategories = categories, value, onChange, disabled = false, label, noneLabel, fallback }: Props) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const id = useId();
    const searchRef = useRef<HTMLInputElement>(null);
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const selected = categories.find(c => c.id === value);
    const selectedPath = useMemo(() => selected ? categoryPath(selected, contextCategories, t) : null,
        [selected, contextCategories, t]);
    const name = selectedPath
        ?? fallback?.categoryName ?? (value ? t('categoryManagement.unavailableCategory') : noneLabel ?? t('transactionManagement.selectCategory'));
    const term = normalizeSearch(search, language);
    // A receipt can contain 100 pickers. Closed pickers need only their selected
    // label, not a sorted/searchable tree on every transaction keystroke.
    const { tree, pins } = useMemo(() => {
        if (!open || disabled) return { tree: [], pins: [] };
        const eligible = new Set(categories.map(c => c.id));
        const matches = (c: CategoryResponse) => eligible.has(c.id) && (!term || normalizeSearch(categoryPath(c, contextCategories, t), language).includes(term));
        return {
            tree: categoryTree(contextCategories, t, language, matches),
            pins: sortCategories(categories.filter(c => c.isPinned && matches(c)), t, language),
        };
    }, [open, disabled, categories, contextCategories, term, t, language]);
    function close() { setOpen(false); setSearch(''); }
    function choose(id: string) { onChange(id); close(); }
    function choice(c: CategoryResponse, shortcut = false, contextOnly = false) {
        return <button type="button" disabled={contextOnly} aria-pressed={value === c.id}
            onClick={() => choose(c.id)}
            className={`flex min-h-12 w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm text-[#152033] hover:bg-primary-500/10 focus-visible:outline-2 focus-visible:outline-primary-500 aria-pressed:bg-primary-500/15 dark:text-[#F3F6FA] ${!c.parentId ? 'bg-primary-500/10 font-bold' : 'font-medium'}`}>
            <span className="text-xl" aria-hidden="true">{c.icon}</span>
            <span className="min-w-0 flex-1 break-words"><span className="block">{getCategoryDisplayName(c, t)}</span>
                {c.parentId && <span className="block text-xs font-normal text-[#667085] dark:text-[#AAB4C4]">{categoryPath(c, contextCategories, t)}</span>}
                <span className="block text-xs font-normal text-primary-700 dark:text-primary-300">{t(c.parentId ? 'categoryManagement.subcategory' : 'categoryManagement.rootCategory')}</span>
            </span>
            {shortcut && <span aria-hidden="true">📌</span>}
            {value === c.id && <span aria-hidden="true">✓</span>}
        </button>;
    }
    return <div className="grid min-w-0 gap-2">
        <AverioButton disabled={disabled} aria-label={label} aria-haspopup="dialog" aria-expanded={open && !disabled}
            aria-describedby={id} className="w-full justify-between text-left" onClick={() => { setOpen(true); setSearch(''); }}>
            <span className="flex min-w-0 items-center gap-2"><span aria-hidden="true">{selected?.icon ?? fallback?.categoryIcon ?? '📂'}</span>
                <span id={id} className="min-w-0 break-words">{name}</span>
                {selected?.isPinned && <span aria-hidden="true">📌</span>}
            </span><span aria-hidden="true">▾</span>
        </AverioButton>
        {open && !disabled && <AverioDialog open title={label} closeLabel={t('categoryManagement.cancel')}
            initialFocusRef={searchRef} onDismiss={close}
            actions={<AverioButton onClick={close}>{t('categoryManagement.cancel')}</AverioButton>}>
            <div className="grid gap-4">
                <AverioInput ref={searchRef} type="search" aria-label={t('transactionManagement.categorySearch')}
                    value={search} onChange={e => setSearch(e.target.value)} placeholder={t('transactionManagement.categorySearchPlaceholder')} />
                {noneLabel && !term && <AverioButton onClick={() => choose('')} aria-pressed={!value}>{noneLabel}</AverioButton>}
                {pins.length > 0 && <AverioCard><section aria-label={t('categoryManagement.pinned')}>
                    <h3 className="mb-2 font-bold">{t('categoryManagement.pinned')}</h3>
                    {pins.map(c => <div key={c.id}>{choice(c, true)}</div>)}
                </section></AverioCard>}
                {tree.length > 0 ? <AverioCard><section aria-label={t('categoryManagement.allCategories')}>
                    <h3 className="mb-2 font-bold">{t('categoryManagement.allCategories')}</h3>
                    <ul className="grid list-none gap-1 p-0">{tree.map(({ category: c, depth, contextOnly }) =>
                        <li key={c.id} className={categoryIndent[Math.min(depth, 4)]}>
                            <div className={depth > 0 ? 'border-l-2 border-primary-500/30 pl-2' : ''}>{choice(c, false, contextOnly)}</div>
                        </li>)}</ul>
                </section></AverioCard> : <p role="status">{t('transactionManagement.categoryNoResults')}</p>}
            </div>
        </AverioDialog>}
        {!selected && fallback && <p className="text-xs text-[#667085] dark:text-[#AAB4C4]">{t('categoryManagement.historicalAssignment')}</p>}
    </div>;
}

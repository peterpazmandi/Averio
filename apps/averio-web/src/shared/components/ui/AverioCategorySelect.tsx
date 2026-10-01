import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { CategoryResponse } from '../../api/generated';
import { getCategoryDisplayName } from '../../../features/transactions/categoryLocalization';
import { normalizeSearch, sortCategories } from '../../../features/categories/categoryOrdering';
import { AverioButton } from './AverioButton';
import { AverioInput } from './AverioFormControls';

interface Props {
    categories: CategoryResponse[];
    value: string;
    onChange: (id: string) => void;
    disabled?: boolean;
    label: string;
    noneLabel?: string;
    fallback?: { categoryName: string; categoryIcon: string };
}

/** Shared category choice: eligible pins first, then localized alphabetical names. */
export function AverioCategorySelect({ categories, value, onChange, disabled = false, label, noneLabel, fallback }: Props) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const id = useId();
    const trigger = useRef<HTMLButtonElement>(null);
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const selected = categories.find(c => c.id === value);
    const selectedParent = categories.find(c => c.id === selected?.parentId);
    const name = selected ? getCategoryDisplayName(selected, t)
        : fallback?.categoryName ?? (value ? t('categoryManagement.unavailableCategory') : noneLabel ?? t('transactionManagement.selectCategory'));
    const byId = new Map(categories.map(c => [c.id, c]));
    const term = normalizeSearch(search, language);
    const choices = sortCategories(categories, t, language).filter(c => {
        const parent = c.parentId ? byId.get(c.parentId) : undefined;
        return normalizeSearch(getCategoryDisplayName(c, t) + ' ' + (parent ? getCategoryDisplayName(parent, t) : ''), language).includes(term);
    });
    function close() { setOpen(false); setSearch(''); trigger.current?.focus(); }
    function choose(id: string) { onChange(id); close(); }
    return <div className="grid min-w-0 gap-2">
        <AverioButton ref={trigger} disabled={disabled} aria-label={label} aria-expanded={open && !disabled}
            aria-controls={id} aria-describedby={`${id}-value`} className="w-full justify-between text-left" onClick={() => { setOpen(!open); setSearch(''); }}>
            <span className="flex min-w-0 items-center gap-2">
                <span aria-hidden="true">{selected?.icon ?? fallback?.categoryIcon ?? '📂'}</span>
                <span id={`${id}-value`} className="min-w-0 break-words">{selectedParent && <span className="block text-xs font-normal">{getCategoryDisplayName(selectedParent, t)}</span>}{name}</span>
                {selected?.isPinned && <span aria-hidden="true">📌</span>}
            </span><span aria-hidden="true">{open ? '▴' : '▾'}</span>
        </AverioButton>
        {open && !disabled && <section id={id} aria-label={label}
            className="grid min-w-0 gap-3 rounded-xl border border-primary-500/30 bg-primary-500/5 p-3"
            onKeyDown={e => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); close(); } }}>
            <AverioInput autoFocus type="search" aria-label={t('transactionManagement.categorySearch')}
                value={search} onChange={e => setSearch(e.target.value)} placeholder={t('transactionManagement.categorySearchPlaceholder')} />
            <div className="grid max-h-72 gap-1 overflow-y-auto overscroll-contain">
                {noneLabel && !term && <AverioButton onClick={() => choose('')} aria-pressed={!value}>{noneLabel}</AverioButton>}
                {choices.length === 0 && <p className="p-3 text-sm">{t('transactionManagement.categoryNoResults')}</p>}
                {choices.map((c, index) => {
                    const parent = c.parentId ? byId.get(c.parentId) : undefined;
                    return <div key={c.id}>
                        {(index === 0 || Boolean(c.isPinned) !== Boolean(choices[index - 1].isPinned)) && <p className="px-2 py-1 text-xs font-bold uppercase tracking-wide text-primary-700 dark:text-primary-300">
                            {t(c.isPinned ? 'categoryManagement.pinned' : 'categoryManagement.allCategories')}
                        </p>}
                        <button type="button" aria-pressed={value === c.id} onClick={() => choose(c.id)}
                            className="flex min-h-12 w-full items-center gap-3 rounded-lg px-3 py-2 text-left text-sm text-[#152033] hover:bg-primary-500/10 focus-visible:outline-2 focus-visible:outline-primary-500 aria-pressed:bg-primary-500/15 dark:text-[#F3F6FA]">
                            <span className="text-xl" aria-hidden="true">{c.icon}</span>
                            <span className="min-w-0 flex-1 break-words"><span className="block font-semibold">{getCategoryDisplayName(c, t)}</span>
                                {parent && <span className="block text-xs text-[#667085] dark:text-[#AAB4C4]">{getCategoryDisplayName(parent, t)}</span>}
                            </span>
                            {c.isPinned && <span aria-label={t('categoryManagement.pinned')}>📌</span>}
                            {value === c.id && <span aria-hidden="true">✓</span>}
                        </button>
                    </div>;
                })}
            </div>
        </section>}
        {!selected && fallback && <p className="text-xs text-[#667085] dark:text-[#AAB4C4]">{t('categoryManagement.historicalAssignment')}</p>}
    </div>;
}

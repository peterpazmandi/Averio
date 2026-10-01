import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';
import type { IconResponse } from '../../api/generated';
import { normalizeSearch } from '../../../features/categories/categoryOrdering';
import { AverioInput } from './AverioFormControls';
import { AverioButton } from './AverioButton';

interface Props { icons: IconResponse[]; value: string; onChange: (value: string) => void; disabled?: boolean; }

/** Named, searchable native radio choices; arrow keys and labels work without custom keyboard emulation. */
export function AverioIconPicker({ icons, value, onChange, disabled = false }: Props) {
    const { t, i18n } = useTranslation();
    const language = i18n.resolvedLanguage ?? i18n.language;
    const id = useId();
    const trigger = useRef<HTMLButtonElement>(null);
    const [open, setOpen] = useState(false);
    const [search, setSearch] = useState('');
    const name = (icon: IconResponse) => t('iconNames.' + icon.id, { defaultValue: icon.name });
    const selected = icons.find(icon => icon.value === value);
    const collator = new Intl.Collator(language, { sensitivity: 'base', numeric: true });
    const term = normalizeSearch(search, language);
    const choices = icons.filter(icon => normalizeSearch(name(icon) + ' ' + icon.name + ' ' + icon.value, language).includes(term))
        .sort((a, b) => collator.compare(name(a), name(b)) || a.id - b.id);
    return <div className="grid min-w-0 gap-3">
        <AverioButton ref={trigger} disabled={disabled} aria-label={t('categoryManagement.chooseIcon')}
            aria-expanded={open && !disabled} aria-controls={id} aria-describedby={`${id}-value`} className="w-full justify-between text-left" onClick={() => setOpen(!open)}>
            <span className="flex min-w-0 items-center gap-3"><span className="text-3xl" aria-hidden="true">{value}</span>
                <span id={`${id}-value`} className="min-w-0 break-words"><span className="block text-xs font-normal">{t('categoryManagement.icon')}</span>
                    {selected ? name(selected) : t('categoryManagement.savedIcon')}</span>
            </span><span aria-hidden="true">{open ? '▴' : '▾'}</span>
        </AverioButton>
        {open && !disabled && <div id={id} className="grid min-w-0 gap-3"
            onKeyDown={e => { if (e.key === 'Escape') { e.preventDefault(); e.stopPropagation(); setOpen(false); trigger.current?.focus(); } }}>
            <AverioInput autoFocus type="search" aria-label={t('categoryManagement.iconSearch')} value={search}
                placeholder={t('categoryManagement.iconSearchHint')} onChange={e => setSearch(e.target.value)} />
            <p className="text-xs" role="status">{t('categoryManagement.iconCount', { count: choices.length })}</p>
            <fieldset className="min-w-0 border-0 p-0">
                <legend className="sr-only">{t('categoryManagement.chooseIcon')}</legend>
                <div className="grid max-h-72 grid-cols-2 gap-2 overflow-y-auto overscroll-contain p-1 min-[400px]:grid-cols-3">
                    {choices.map(icon => <label key={icon.id} className="relative min-w-0 cursor-pointer">
                        <input type="radio" name={id} value={icon.value} checked={value === icon.value} onChange={() => onChange(icon.value)}
                            className="peer sr-only" />
                        <span className="flex h-full min-h-24 flex-col items-center justify-center gap-2 rounded-xl border border-[#E4E7EC] bg-white p-2 text-center text-xs text-[#152033] transition-colors hover:border-primary-500 peer-checked:border-primary-500 peer-checked:bg-primary-500/15 peer-focus-visible:outline-2 peer-focus-visible:outline-offset-2 peer-focus-visible:outline-primary-500 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] dark:peer-checked:bg-primary-500/15">
                            <span className="text-3xl" aria-hidden="true">{icon.value}</span>
                            <span className="w-full break-words font-semibold">{name(icon)}</span>
                            {value === icon.value && <span className="absolute top-1 right-2 text-primary-700 dark:text-primary-300" aria-hidden="true">✓</span>}
                        </span>
                    </label>)}
                </div>
            </fieldset>
            {!choices.length && <p>{t('categoryManagement.noIcons')}</p>}
            <AverioButton onClick={() => { setOpen(false); trigger.current?.focus(); }}>{t('categoryManagement.done')}</AverioButton>
        </div>}
    </div>;
}

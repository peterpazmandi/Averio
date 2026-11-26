import { useId, useState, type ReactNode } from 'react';
import { AverioCard } from './AverioCard';

/** Compact optional form group; collapsed controls stay mounted to preserve drafts. */
export function AverioDisclosure({ title, summary, help, defaultOpen = false, children }: {
    title: string; summary?: string; help?: ReactNode; defaultOpen?: boolean; children: ReactNode;
}) {
    const [open, setOpen] = useState(defaultOpen);
    const id = useId();
    return <AverioCard className="py-2!">
        <div className={help ? 'grid grid-cols-[minmax(0,max-content)_44px_minmax(16px,1fr)] items-start' : ''}>
            {help && <span aria-hidden="true" className="invisible col-start-1 row-start-1 overflow-hidden px-0 pt-3 font-semibold whitespace-nowrap">{title}</span>}
            <button type="button" aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}
                className={'flex min-h-11 w-full justify-between gap-3 rounded-lg text-left focus-visible:outline-2 focus-visible:outline-emerald-500 ' + (help ? 'col-span-3 col-start-1 row-start-1 items-start py-3' : 'items-center')}>
                <span className={'min-w-0 ' + (help ? 'max-w-[calc(100%-60px)]' : '')}><span className="block font-semibold">{title}</span>
                    {summary && <span className={'block text-xs break-words text-slate-600 dark:text-slate-300 ' + (help ? 'mt-4' : 'truncate')}>{summary}</span>}</span>
                <span aria-hidden="true">{open ? '−' : '+'}</span>
            </button>
            {help && <span className="z-1 col-start-2 row-start-1">{help}</span>}
        </div>
        <div id={id} hidden={!open} onInvalidCapture={() => setOpen(true)}>
            <div className="grid gap-3 pb-2 pt-3">{children}</div>
        </div>
    </AverioCard>;
}

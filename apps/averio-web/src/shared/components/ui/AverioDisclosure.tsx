import { useId, useState, type ReactNode } from 'react';
import { AverioCard } from './AverioCard';

/** Compact optional form group; collapsed controls stay mounted to preserve drafts. */
export function AverioDisclosure({ title, summary, defaultOpen = false, children }: {
    title: string; summary?: string; defaultOpen?: boolean; children: ReactNode;
}) {
    const [open, setOpen] = useState(defaultOpen);
    const id = useId();
    return <AverioCard className="py-2!">
        <button type="button" aria-expanded={open} aria-controls={id} onClick={() => setOpen(!open)}
            className="flex min-h-11 w-full items-center justify-between gap-3 rounded-lg text-left focus-visible:outline-2 focus-visible:outline-emerald-500">
            <span className="min-w-0"><span className="block font-semibold">{title}</span>
                {summary && <span className="block truncate text-xs text-slate-600 dark:text-slate-300">{summary}</span>}</span>
            <span aria-hidden="true">{open ? '−' : '+'}</span>
        </button>
        <div id={id} hidden={!open} onInvalidCapture={() => setOpen(true)}>
            <div className="grid gap-3 pb-2 pt-3">{children}</div>
        </div>
    </AverioCard>;
}

import { useId, useRef, useState, type ReactNode } from 'react';
import { ConceptIcon } from '../concept/ConceptIcon';
import { AverioButton } from './AverioButton';

interface Props {
    children: ReactNode;
    summary: ReactNode;
    count: number;
    labels: { filters: string; showResults: string; reset: string; summary: string };
    onReset: () => void;
}

/** Collapsible filters preserve selections and restore focus when dismissed. */
export function AverioFilterPanel({ children, summary, count, labels, onReset }: Props) {
    const [open, setOpen] = useState(false);
    const id = useId();
    const toggle = useRef<HTMLButtonElement>(null);
    return <div className="grid min-w-0 gap-2.5">
        <AverioButton ref={toggle} className="justify-self-start aria-expanded:border-primary-500" aria-expanded={open}
            aria-controls={id} onClick={() => setOpen(value => !value)}>
            <ConceptIcon name="filters" />{labels.filters}
            {count > 0 && <span className="rounded-md bg-primary-500/15 px-1.5 py-0.5">{count}</span>}
        </AverioButton>
        <div id={id} hidden={!open} className="rounded-[14px] border border-[#E4E7EC] bg-white p-[18px] dark:border-[#233044] dark:bg-[#0D1726]">
            {children}
            <AverioButton className="mt-3 max-[760px]:w-full" onClick={() => {
                setOpen(false);
                toggle.current?.focus();
            }}>{labels.showResults}</AverioButton>
        </div>
        <div aria-label={labels.summary} className="flex flex-wrap items-center justify-between gap-2 text-xs text-[#667085] dark:text-[#AAB4C4]">
            <div className="flex min-w-0 flex-wrap gap-2">{summary}</div>
            {count > 0 && <AverioButton onClick={onReset}>{labels.reset}</AverioButton>}
        </div>
    </div>;
}

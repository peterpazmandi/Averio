import { useState, type ReactNode } from 'react';
import { ConceptIcon } from '../concept/ConceptIcon';
import { AverioButton } from './AverioButton';
import { AverioDialog } from './AverioDialog';

interface Props {
    children: ReactNode;
    summary: ReactNode;
    count: number;
    labels: { filters: string; showResults: string; reset: string; summary: string; close: string };
    onReset: () => void;
}

/** Compact toolbar trigger; selections apply immediately and persist on dismissal. */
export function AverioFilterPanel({ children, summary, count, labels, onReset }: Props) {
    const [open, setOpen] = useState(false);
    return <>
        <AverioButton aria-haspopup="dialog" onClick={() => setOpen(true)}>
            <ConceptIcon name="filters" />{labels.filters}
            {count > 0 && <span className="rounded-md bg-primary-500/15 px-1.5 py-0.5">{count}</span>}
        </AverioButton>
        {open && <AverioDialog open title={labels.filters} closeLabel={labels.close} onDismiss={() => setOpen(false)}
            actions={<>
                <AverioButton disabled={count === 0} onClick={onReset}>{labels.reset}</AverioButton>
                <AverioButton variant="primary" onClick={() => setOpen(false)}>{labels.showResults}</AverioButton>
            </>}>
            {children}
            <div aria-label={labels.summary} className="mt-4 flex min-w-0 flex-wrap gap-2 border-t border-[#E4E7EC] pt-4 text-xs dark:border-[#233044]">{summary}</div>
        </AverioDialog>}
    </>;
}

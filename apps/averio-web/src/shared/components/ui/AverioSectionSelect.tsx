import type { ComponentProps } from 'react';
import { ConceptIcon } from '../concept/ConceptIcon';
import { AverioField, AverioSelect } from './AverioFormControls';

/** Compact section navigation with native keyboard and mobile picker behavior. */
export function AverioSectionSelect<T extends string>({ label, value, items, onChange }: {
    label: string;
    value: T;
    items: { value: T; label: string; icon: ComponentProps<typeof ConceptIcon>['name'] }[];
    onChange: (value: T) => void;
}) {
    const selected = items.find(item => item.value === value);
    return <AverioField>
        {label}
        <span className="relative block min-w-0">
            <span className="pointer-events-none absolute inset-y-0 left-3 flex items-center text-emerald-700 dark:text-emerald-300">
                {selected && <ConceptIcon name={selected.icon} />}
            </span>
            <AverioSelect value={value} onChange={event => {
                const item = items.find(item => item.value === event.target.value);
                if (item) onChange(item.value);
            }} className="min-h-12 border-emerald-300! bg-emerald-50! pl-10 text-emerald-950! dark:border-emerald-800! dark:bg-emerald-950! dark:text-emerald-100!">
                {items.map(item => <option key={item.value} value={item.value}>{item.label}</option>)}
            </AverioSelect>
        </span>
    </AverioField>;
}

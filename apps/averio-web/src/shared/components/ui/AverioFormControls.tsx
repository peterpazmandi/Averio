import type { ComponentProps } from 'react';

const controlClasses = 'min-h-11 w-full min-w-0 max-w-full rounded-[11px] border border-[#E4E7EC] bg-white px-3 py-2 text-sm! font-normal! text-[#152033] transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-55 aria-invalid:border-red-500 dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] motion-reduce:transition-none';

/** Wrap one control in a native label, or use htmlFor with a separate control. */
export function AverioField({ className = '', ...props }: ComponentProps<'label'>) {
    return <label {...props} className={`grid min-w-0 gap-[7px] text-[13px] font-semibold ${className}`} />;
}

export function AverioInput({ className = '', ...props }: ComponentProps<'input'>) {
    return <input {...props} className={`${controlClasses} ${className}`} />;
}

export function AverioSelect({ className = '', ...props }: ComponentProps<'select'>) {
    return <select {...props} className={`${controlClasses} ${className}`} />;
}

export function AverioTextarea({ className = '', ...props }: ComponentProps<'textarea'>) {
    return <textarea {...props} className={`${controlClasses} min-h-[90px] resize-y ${className}`} />;
}

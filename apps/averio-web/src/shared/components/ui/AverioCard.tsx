import type { ComponentProps } from 'react';

/** One menu-inspired surface for a related group of content or form controls. */
export function AverioCard({ className = '', ...props }: ComponentProps<'div'>) {
    return <div {...props} className={`min-w-0 rounded-[17px] border border-[#E4E7EC] border-l-[3px] border-l-primary-500 bg-white p-4 text-[#152033] shadow-sm dark:border-[#233044] dark:border-l-primary-500 dark:bg-[#0D1726] dark:text-[#F3F6FA] ${className}`} />;
}

import type { ComponentProps } from 'react';

interface AverioButtonProps extends ComponentProps<'button'> {
    size?: 'default' | 'icon';
    variant?: 'secondary' | 'primary' | 'danger';
}

const variants = {
    secondary: 'border-[#E4E7EC] bg-white text-[#152033] hover:bg-[#F9FAFB] dark:border-[#233044] dark:bg-[#0D1726] dark:text-[#F3F6FA] dark:hover:bg-[#111C2F]',
    primary: 'border-primary-500 bg-primary-500 text-white shadow-[0_6px_18px_rgba(16,185,129,0.18)] hover:border-primary-600 hover:bg-primary-600',
    danger: 'border-red-700 bg-red-700 text-white hover:border-red-800 hover:bg-red-800',
};

/** Native button semantics, including form association, refs and accessible names. */
export function AverioButton({ variant = 'secondary', size = 'default', type = 'button', className = '', ...props }: AverioButtonProps) {
    return <button {...props} type={type}
        className={`inline-flex min-h-11 items-center justify-center gap-2 rounded-[11px] border text-xs! font-semibold! shadow-sm transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 disabled:cursor-not-allowed disabled:opacity-55 motion-reduce:transition-none ${size === 'icon' ? 'size-11 shrink-0 p-0' : 'px-[13px] py-2'} ${variants[variant]} ${className}`} />;
}

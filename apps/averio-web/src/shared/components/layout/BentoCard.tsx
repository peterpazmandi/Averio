import type {
    HTMLAttributes,
    PropsWithChildren,
} from 'react';

interface BentoCardProps
    extends PropsWithChildren,
        HTMLAttributes<HTMLDivElement> {
    interactive?: boolean;
}

export function BentoCard({
    children,
    interactive = false,
    className = '',
    ...props
}: BentoCardProps) {
    return (
        <div
            {...props}
            className={`
                rounded-3xl
                border border-slate-200
                bg-white
                shadow-sm

                transition-colors

                dark:border-slate-800
                dark:bg-slate-900

                ${
                    interactive
                        ? `
                            transition-all
                            hover:border-slate-300
                            hover:shadow-md

                            dark:hover:border-slate-700
                            dark:hover:bg-slate-800/80
                        `
                        : ''
                }

                ${className}
            `}
        >
            {children}
        </div>
    );
}
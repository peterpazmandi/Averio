import { AverioLogo } from './AverioLogo';

type AverioBrandVariant =
    | 'default'
    | 'on-dark';

interface AverioBrandProps {
    compact?: boolean;
    variant?: AverioBrandVariant;
    className?: string;
}

const wordmarkClasses: Record<
    AverioBrandVariant,
    string
> = {
    default: `
        text-brand-navy
        dark:text-white
    `,

    'on-dark': `
        text-white
    `,
};

export function AverioBrand({
    compact = false,
    variant = 'default',
    className = '',
}: AverioBrandProps) {
    return (
        <div
            className={`
                flex
                items-center
                gap-3
                ${className}
            `}
        >
            <div
                className="
                    flex
                    size-12
                    shrink-0
                    items-center
                    justify-center

                    rounded-2xl

                    border
                    border-white/10

                    bg-slate-900/80

                    p-2

                    shadow-sm
                "
            >
                <AverioLogo className="h-9 w-auto" />
            </div>

            {!compact && (
                <span className={`text-xl font-semibold tracking-tight ${wordmarkClasses[variant]} `} >
                    Averio
                </span>
            )}
        </div>
    );
}
import type {
    ButtonHTMLAttributes,
} from 'react';

interface PrimaryButtonProps
    extends ButtonHTMLAttributes<HTMLButtonElement> {
    loading?: boolean;
}

export function PrimaryButton({
    children,
    loading = false,
    disabled,
    ...props
}: PrimaryButtonProps) {
    return (
        <button
            {...props}
            disabled={disabled || loading}
            className="
                flex
                w-full
                items-center
                justify-center
                rounded-xl
                bg-primary-500
                px-4
                py-3
                font-semibold
                text-slate-950
                transition
                hover:bg-primary-400
                focus:outline-none
                focus:ring-4
                focus:ring-primary-200
                disabled:cursor-not-allowed
                disabled:opacity-60
                dark:hover:bg-primary-400
                dark:focus:ring-primary-900
            "
        >
            {loading ? (
                <>
                    <span className="sr-only">{children}</span>
                    <span aria-hidden="true">…</span>
                </>
            ) : (
                children
            )}
        </button>
    );
}
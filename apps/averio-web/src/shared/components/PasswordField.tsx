import {
    useState,
    type InputHTMLAttributes,
} from 'react';

import {
    useTranslation,
} from 'react-i18next';

interface PasswordFieldProps
    extends Omit<
        InputHTMLAttributes<HTMLInputElement>,
        'type'
    > {
    label: string;
    error?: string;
}

export function PasswordField({
    label,
    error,
    ...props
}: PasswordFieldProps) {
    const { t } =
        useTranslation();

    const [
        visible,
        setVisible,
    ] = useState(false);

    return (
        <label className="block">
            <span
                className="
                    text-sm font-medium
                    text-slate-700
                    dark:text-slate-300
                "
            >
                {label}
            </span>

            <div className="relative mt-2">
                <input
                    {...props}
                    type={
                        visible
                            ? 'text'
                            : 'password'
                    }
                    className={`
                        w-full rounded-xl
                        border bg-white
                        px-4 py-3 pr-12
                        text-slate-950
                        outline-none
                        transition

                        placeholder:text-slate-400

                        focus:border-primary-500
                        focus:ring-4
                        focus:ring-primary-100

                        dark:bg-slate-950
                        dark:text-white
                        dark:placeholder:text-slate-600
                        dark:focus:ring-primary-950

                        ${
                            error
                                ? 'border-red-400'
                                : `
                                    border-slate-300
                                    dark:border-slate-700
                                `
                        }
                    `}
                />

                <button
                    type="button"
                    onClick={() =>
                        setVisible(
                            value =>
                                !value,
                        )
                    }
                    aria-label={
                        visible
                            ? t(
                                  'password.hide',
                              )
                            : t(
                                  'password.show',
                              )
                    }
                    className="
                        absolute right-2 top-1/2
                        flex size-9
                        -translate-y-1/2
                        items-center
                        justify-center
                        rounded-lg
                        text-slate-400
                        transition
                        hover:bg-slate-100
                        hover:text-slate-700
                        dark:hover:bg-slate-800
                        dark:hover:text-white
                    "
                >
                    {visible
                        ? <EyeOffIcon />
                        : <EyeIcon />}
                </button>
            </div>

            {error && (
                <span
                    className="
                        mt-1.5 block
                        text-sm text-red-600
                        dark:text-red-400
                    "
                >
                    {error}
                </span>
            )}
        </label>
    );
}

function EyeIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
        >
            <path d="M2 12s3.5-6 10-6 10 6 10 6-3.5 6-10 6S2 12 2 12Z" />
            <circle
                cx="12"
                cy="12"
                r="3"
            />
        </svg>
    );
}

function EyeOffIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-5"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
        >
            <path d="m3 3 18 18" />
            <path d="M10.6 10.6a2 2 0 0 0 2.8 2.8" />
            <path d="M9.9 4.2A10.8 10.8 0 0 1 12 4c6.5 0 10 8 10 8a18 18 0 0 1-2.2 3.1" />
            <path d="M6.6 6.6C3.6 8.6 2 12 2 12s3.5 8 10 8a10.6 10.6 0 0 0 5.4-1.5" />
        </svg>
    );
}
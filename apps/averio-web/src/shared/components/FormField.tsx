import type { InputHTMLAttributes } from 'react';

interface FormFieldProps
    extends InputHTMLAttributes<HTMLInputElement> {
    label: string;
    error?: string;
}

export function FormField({
    label,
    error,
    ...props
}: FormFieldProps) {
    return (
        <label className="block">
            <span className="text-sm font-medium text-slate-700 dark:text-slate-300" >
                {label}
            </span>

            <input
                {...props}
                className={`
                    mt-2
                    w-full

                    rounded-xl
                    border

                    bg-white
                    text-slate-950

                    px-4
                    py-3

                    outline-none
                    transition

                    placeholder:text-slate-400

                    border-slate-300

                    hover:border-slate-400

                    focus:border-primary-500
                    focus:ring-4
                    focus:ring-primary-100

                    dark:border-slate-700
                    dark:bg-slate-950
                    dark:text-white
                    dark:placeholder:text-slate-600

                    dark:hover:border-slate-600

                    dark:focus:border-primary-500
                    dark:focus:ring-primary-950

                    ${
                        error
                            ? `
                                border-red-400
                                dark:border-red-500
                            `
                            : ''
                    }
                `}
            />

            {error && (
                <span
                    className="
                        mt-1.5
                        block
                        text-sm

                        text-red-600
                        dark:text-red-400
                    "
                >
                    {error}
                </span>
            )}
        </label>
    );
}
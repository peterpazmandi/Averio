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
            <span className="text-sm font-medium text-slate-700" >
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
                    px-4
                    py-3
                    text-slate-950
                    outline-none
                    transition
                    placeholder:text-slate-400
                    focus:border-primary-500
                    focus:ring-4
                    focus:ring-primary-100
                    ${
                        error
                            ? 'border-red-400'
                            : 'border-slate-300'
                    }
                `}
            />

            {error && (
                <span className="
                        mt-1.5
                        block
                        text-sm
                        text-red-600"
                >
                    {error}
                </span>
            )}
        </label>
    );
}
import { AverioLogo } from './AverioLogo';

interface AverioBrandProps {
    compact?: boolean;
}

export function AverioBrand({
    compact = false,
}: AverioBrandProps) {
    return (
        <div className="flex items-center gap-3">
            <div
                className="
                    flex size-12 items-center justify-center
                    rounded-2xl border border-slate-800
                    bg-slate-950 p-2
                    shadow-sm
                "
            >
                <AverioLogo className="h-9 w-auto" />
            </div>

            {!compact && (
                <span
                    className="
                        text-xl font-semibold tracking-tight
                        text-slate-950
                        dark:text-white
                    "
                >
                    Averio
                </span>
            )}
        </div>
    );
}
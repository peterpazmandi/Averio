import { AverioLogo } from '../../shared/components/branding/AverioLogo';
export function ActivityIllustration() {
    return <div aria-hidden="true" className="relative mx-auto mb-5 h-28 w-48">
        <div className="absolute top-1 left-17 h-9 w-14 rounded-t-xl border-4 border-emerald-400" />
        <div className="absolute inset-x-2 top-7 bottom-1 rounded-2xl border-2 border-emerald-300 bg-emerald-50 dark:border-emerald-700 dark:bg-emerald-950">
            <div className="absolute inset-y-0 left-6 w-3 border-x border-emerald-300 dark:border-emerald-700" />
            <div className="absolute inset-y-0 right-6 w-3 border-x border-emerald-300 dark:border-emerald-700" />
            <AverioLogo className="mx-auto mt-4 h-10 w-10" />
        </div>
    </div>;
}

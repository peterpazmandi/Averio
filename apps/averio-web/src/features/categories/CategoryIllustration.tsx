import { AverioLogo } from '../../shared/components/branding/AverioLogo';

export function CategoryIllustration() {
    return <div aria-hidden="true" className="relative mx-auto mb-5 flex h-28 w-48 items-center justify-center">
        <div className="absolute inset-x-3 bottom-1 h-20 rounded-2xl border-2 border-primary-500/40 bg-primary-500/10" />
        <div className="absolute left-6 top-2 h-6 w-16 rounded-t-lg bg-primary-500/25" />
        <div className="relative flex items-center gap-3">
            <AverioLogo className="h-14 w-10" />
            <div className="grid gap-2">
                <span className="h-2 w-14 rounded bg-primary-500/60" />
                <span className="h-2 w-10 rounded bg-primary-500/30" />
                <span className="h-2 w-12 rounded bg-primary-500/30" />
            </div>
        </div>
    </div>;
}

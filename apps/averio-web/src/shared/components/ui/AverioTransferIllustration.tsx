import { AverioLogo } from '../branding/AverioLogo';

/** Decorative account movement artwork; accompanying copy conveys the meaning. */
export function AverioTransferIllustration() {
    return <div aria-hidden="true" className="pointer-events-none relative mx-auto w-64 max-w-full select-none">
        <svg viewBox="0 0 280 180" fill="none" className="h-auto w-full" focusable="false">
            <ellipse cx="140" cy="94" rx="119" ry="73" className="fill-primary-500/5 dark:fill-primary-500/10" />
            <ellipse cx="140" cy="157" rx="99" ry="8" className="fill-slate-200/60 dark:fill-slate-950/40" />
            <g strokeWidth="2" strokeLinejoin="round">
                <path d="M29 72a12 12 0 0 1 12-12h70v69H41a12 12 0 0 1-12-12Z" className="fill-primary-100 stroke-primary-300 dark:fill-[#163B36] dark:stroke-primary-700" />
                <rect x="29" y="73" width="88" height="65" rx="12" className="fill-white stroke-slate-200 dark:fill-[#142234] dark:stroke-slate-600" />
                <path d="M95 94h22v25H95a8 8 0 0 1-8-8v-9a8 8 0 0 1 8-8Z" className="fill-primary-100 stroke-primary-300 dark:fill-[#163B36] dark:stroke-primary-700" />
                <circle cx="99" cy="106" r="3" className="fill-primary-500 stroke-primary-500" />
                <path d="M168 89a12 12 0 0 1 12-12h70v69h-70a12 12 0 0 1-12-12Z" className="fill-primary-100 stroke-primary-300 dark:fill-[#163B36] dark:stroke-primary-700" />
                <rect x="168" y="90" width="88" height="65" rx="12" className="fill-white stroke-slate-200 dark:fill-[#142234] dark:stroke-slate-600" />
                <path d="M234 111h22v25h-22a8 8 0 0 1-8-8v-9a8 8 0 0 1 8-8Z" className="fill-primary-100 stroke-primary-300 dark:fill-[#163B36] dark:stroke-primary-700" />
                <circle cx="238" cy="123" r="3" className="fill-primary-500 stroke-primary-500" />
            </g>
            <path d="M90 47c36-27 84-17 112 12m-1-15 2 16-16-2" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" className="stroke-primary-500" />
            <path d="M132 125h17m-6-6 6 6-6 6" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round" className="stroke-primary-500" />
            <circle cx="44" cy="40" r="3" className="fill-primary-300 dark:fill-primary-700" />
            <circle cx="242" cy="64" r="4" className="fill-primary-200 dark:fill-primary-800" />
        </svg>
        <span className="absolute top-[49%] left-[16%] grid size-9 place-items-center rounded-xl bg-[#142234]">
            <AverioLogo className="h-7 w-auto" />
        </span>
    </div>;
}

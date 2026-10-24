import { useEffect, useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { AverioBrand } from '../../shared/components/branding/AverioBrand';
import { useTheme } from '../../shared/theme/ThemeProvider';

const featureCards = [
    { icon: '⌂', title: 'householdTitle', description: 'householdDescription', detail: 'householdDetail' },
    { icon: '◈', title: 'personalTitle', description: 'personalDescription', detail: 'personalDetail' },
    { icon: '▥', title: 'reportTitle', description: 'reportDescription', detail: 'reportDetail' },
] as const;

const capabilities = [
    { icon: '▤', title: 'accounts', text: 'accountsDescription' },
    { icon: '⇄', title: 'transfers', text: 'transfersDescription' },
    { icon: '≡', title: 'transactions', text: 'transactionsDescription' },
    { icon: '↻', title: 'recurring', text: 'recurringDescription' },
    { icon: '◫', title: 'categories', text: 'categoriesDescription' },
    { icon: '◎', title: 'activities', text: 'activitiesDescription' },
] as const;

const steps = ['stepOne', 'stepTwo', 'stepThree'] as const;
const questions = ['solo', 'private', 'banks', 'currencies', 'pricing'] as const;

const primaryLink = 'inline-flex min-h-12 items-center justify-center gap-2 rounded-xl bg-primary-700 px-6 py-3 text-sm font-bold text-white shadow-[0_12px_30px_rgba(16,185,129,0.2)] transition hover:bg-primary-800 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 motion-reduce:transition-none';
const secondaryLink = 'inline-flex min-h-12 items-center justify-center gap-2 rounded-xl border border-slate-200 bg-white px-6 py-3 text-sm font-semibold text-slate-800 transition hover:border-primary-300 hover:bg-primary-50 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary-500 dark:border-white/20 dark:bg-white/5 dark:text-white dark:hover:bg-white/10';
const surface = 'rounded-[24px] border border-[#e3ece7] bg-white shadow-[0_12px_40px_rgba(26,55,50,0.04)] dark:border-white/10 dark:bg-[#0d1b2a]';
const bodyText = 'text-slate-600 dark:text-slate-300';

function LandingPreview() {
    const { t } = useTranslation();
    const [view, setView] = useState<'household' | 'personal'>('household');
    const personal = view === 'personal';

    return (
        <div className="relative mx-auto w-full max-w-[600px]">
            <div aria-hidden="true" className="pointer-events-none absolute -inset-8 rounded-full bg-primary-400/10 blur-3xl" />
            <div className="relative overflow-hidden rounded-[23px] border border-slate-200/90 bg-[#f7fafb] shadow-[0_30px_90px_rgba(9,36,43,0.16)] dark:border-white/10 dark:bg-[#101d2c]">
                <div className="flex items-center justify-between gap-3 border-b border-slate-200 bg-white px-4 py-3 dark:border-white/10 dark:bg-[#132434] sm:px-5">
                    <div className="flex items-center gap-2"><span className="size-2.5 rounded-full bg-rose-300" /><span className="size-2.5 rounded-full bg-amber-300" /><span className="size-2.5 rounded-full bg-emerald-400" /></div>
                    <span className="truncate text-[10px] font-semibold text-slate-400">app.averio · {t('landing.previewLabel')}</span>
                    <span className="size-6 rounded-full bg-primary-100 text-center text-xs font-bold leading-6 text-primary-700 dark:bg-primary-500/20 dark:text-primary-200">A</span>
                </div>
                <div className="grid grid-cols-1 sm:grid-cols-[110px_minmax(0,1fr)]">
                    <aside aria-hidden="true" className="hidden min-h-[400px] flex-col gap-3 bg-[#0b1c2a] p-4 text-[10px] text-slate-400 sm:flex">
                        <div className="mb-4 text-sm font-black tracking-tight text-white">averio<span className="text-primary-400">.</span></div>
                        <div className="rounded-lg bg-primary-500/15 px-2 py-2 font-bold text-emerald-300">▦ &nbsp; {t('landing.previewOverview')}</div>
                        <div className="px-2 py-1.5">▤ &nbsp; {t('landing.previewAccounts')}</div>
                        <div className="px-2 py-1.5">⇄ &nbsp; {t('landing.previewActivity')}</div>
                        <div className="px-2 py-1.5">▥ &nbsp; {t('landing.previewReports')}</div>
                    </aside>
                    <div className="min-w-0 p-4 sm:p-5">
                        <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                            <div><p className="text-[10px] font-medium text-slate-500 dark:text-slate-400">{t('landing.previewEyebrow')}</p><h3 className="text-lg font-extrabold tracking-tight text-[#142b37] dark:text-white">{t('landing.previewWelcome')}</h3></div>
                            <div className="flex rounded-lg border border-slate-200 bg-white p-1 dark:border-white/10 dark:bg-white/5" role="group" aria-label={t('landing.previewSwitchLabel')}>
                                <button type="button" aria-pressed={!personal} onClick={() => setView('household')} className={'rounded-md px-2.5 py-1.5 text-[10px] font-bold transition focus-visible:outline-2 focus-visible:outline-primary-500 ' + (!personal ? 'bg-primary-700 text-white' : 'text-slate-500 dark:text-slate-300')}>{t('landing.householdTab')}</button>
                                <button type="button" aria-pressed={personal} onClick={() => setView('personal')} className={'rounded-md px-2.5 py-1.5 text-[10px] font-bold transition focus-visible:outline-2 focus-visible:outline-primary-500 ' + (personal ? 'bg-primary-700 text-white' : 'text-slate-500 dark:text-slate-300')}>{t('landing.personalTab')}</button>
                            </div>
                        </div>
                        <div className="rounded-2xl bg-[#112b37] p-5 text-white">
                            <p className="text-[11px] font-semibold text-emerald-100/75">{t(personal ? 'landing.allowanceRemaining' : 'landing.householdBalance')}</p>
                            <div className="mt-1 flex flex-wrap items-end justify-between gap-3"><strong className="text-[32px] font-extrabold tracking-tight">{personal ? '€312.00' : '€14,382.50'}</strong><span className="rounded-full bg-emerald-400/15 px-2.5 py-1 text-[10px] font-semibold text-emerald-300">{t(personal ? 'landing.examplePersonalTag' : 'landing.exampleSharedTag')}</span></div>
                            <p className="mt-2 text-[10px] text-slate-300">{t(personal ? 'landing.previewPersonalHint' : 'landing.previewHouseholdHint')}</p>
                        </div>
                        <div className="mt-3 grid grid-cols-2 gap-3">
                            <div className="rounded-xl border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-[#182b39]"><span className="text-[10px] text-slate-500 dark:text-slate-400">{t(personal ? 'landing.allocation' : 'landing.income')}</span><strong className="mt-2 block text-base font-bold text-emerald-600 dark:text-emerald-300">{personal ? '€600' : '€5,480'}</strong></div>
                            <div className="rounded-xl border border-slate-200 bg-white p-3 dark:border-white/10 dark:bg-[#182b39]"><span className="text-[10px] text-slate-500 dark:text-slate-400">{t(personal ? 'landing.used' : 'landing.expenses')}</span><strong className="mt-2 block text-base font-bold text-[#162f3c] dark:text-white">{personal ? '€288' : '€3,312'}</strong></div>
                        </div>
                        <div className="mt-3 rounded-xl border border-slate-200 bg-white p-4 dark:border-white/10 dark:bg-[#182b39]">
                            <div className="flex items-center justify-between gap-2"><strong className="text-[11px] text-[#182f3c] dark:text-white">{t(personal ? 'landing.allowanceHistory' : 'landing.cashFlow')}</strong><span className="text-[10px] text-slate-400">{t('landing.thisMonth')}</span></div>
                            {personal ? (
                                <div className="mt-4 space-y-3 text-[11px]">
                                    <div className="flex justify-between gap-2"><span className={bodyText}>+ {t('landing.sampleAllowance')}</span><strong className="text-emerald-600 dark:text-emerald-300">+€300</strong></div>
                                    <div className="flex justify-between gap-2"><span className={bodyText}>− {t('landing.samplePurchase')}</span><strong className="text-rose-500">−€48</strong></div>
                                    <div className="h-2 overflow-hidden rounded-full bg-slate-100 dark:bg-white/10"><div className="h-full w-[52%] rounded-full bg-primary-500" /></div>
                                </div>
                            ) : (
                                <div className="mt-5 flex h-[95px] items-end justify-between gap-2 border-b border-slate-100 dark:border-white/10" role="img" aria-label={t('landing.chartDescription')}>
                                    {[
                                        ['h-[64%]', 'h-[38%]'], ['h-[73%]', 'h-[47%]'], ['h-[70%]', 'h-[40%]'],
                                        ['h-[85%]', 'h-[53%]'], ['h-[77%]', 'h-[44%]'], ['h-[92%]', 'h-[49%]'],
                                    ].map((bar, index) => (
                                        <div className="flex h-full flex-1 items-end justify-center gap-1" key={index}>
                                            <span className={'w-3 rounded-t bg-primary-500 ' + bar[0]} />
                                            <span className={'w-3 rounded-t bg-[#a2b6e9] ' + bar[1]} />
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            </div>
            <p className="mt-3 text-center text-[11px] text-slate-500 dark:text-slate-400">{t('landing.illustrativeData')}</p>
        </div>
    );
}

export function LandingPage() {
    const { t, i18n } = useTranslation();
    const { theme, toggleTheme } = useTheme();
    const [menuOpen, setMenuOpen] = useState(false);

    useEffect(() => {
        const previousTitle = document.title;
        document.title = t('landing.documentTitle');
        return () => { document.title = previousTitle; };
    }, [t, i18n.resolvedLanguage]);

    function closeMenu() { setMenuOpen(false); }

    return (
        <div className="min-h-screen overflow-x-clip bg-[#f7faf8] text-[#152e3a] dark:bg-[#07101f] dark:text-[#f1f6f4]">
            <a href="#main-content" className="sr-only rounded-lg bg-white p-3 text-primary-700 focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-[100]">{t('landing.skipContent')}</a>
            <header className="sticky top-0 z-50 border-b border-[#e3ebe7]/90 bg-[#f7faf8]/95 backdrop-blur-xl dark:border-white/10 dark:bg-[#07101f]/95">
                <div className="mx-auto flex min-h-[76px] max-w-[1250px] items-center justify-between gap-4 px-5 sm:px-8 lg:px-10">
                    <a href="#top" className="shrink-0 rounded-lg focus-visible:outline-2 focus-visible:outline-primary-500" onClick={closeMenu} aria-label={t('landing.homeLabel')}><AverioBrand /></a>
                    <nav className="hidden items-center gap-8 text-[13px] font-semibold text-slate-600 dark:text-slate-300 lg:flex" aria-label={t('landing.mainNavigation')}>
                        <a className="hover:text-primary-600" href="#why-averio">{t('landing.whyNav')}</a>
                        <a className="hover:text-primary-600" href="#features">{t('landing.featuresNav')}</a>
                        <a className="hover:text-primary-600" href="#how-it-works">{t('landing.howNav')}</a>
                        <a className="hover:text-primary-600" href="#faq">{t('landing.faqNav')}</a>
                    </nav>
                    <div className="ml-auto flex items-center gap-2">
                        <select className="h-10 rounded-lg border border-slate-200 bg-white px-2 text-xs font-semibold text-slate-700 focus-visible:outline-2 focus-visible:outline-primary-500 dark:border-white/20 dark:bg-[#132434] dark:text-white" aria-label={t('landing.languageLabel')} value={(i18n.resolvedLanguage ?? i18n.language).split('-')[0]} onChange={(event) => { void i18n.changeLanguage(event.target.value); }}>
                            <option value="en">EN</option><option value="de">DE</option><option value="hu">HU</option>
                        </select>
                        <button type="button" onClick={toggleTheme} aria-label={t(theme === 'dark' ? 'landing.lightMode' : 'landing.darkMode')} title={t(theme === 'dark' ? 'landing.lightMode' : 'landing.darkMode')} className="grid size-10 place-items-center rounded-lg border border-slate-200 bg-white text-lg focus-visible:outline-2 focus-visible:outline-primary-500 dark:border-white/20 dark:bg-[#132434]">{theme === 'dark' ? '☀' : '☾'}</button>
                        <Link to="/login" search={{ redirect: undefined }} className="hidden px-3 text-[13px] font-bold hover:text-primary-600 sm:block">{t('landing.signIn')}</Link>
                        <Link to="/register" className="hidden min-h-10 items-center rounded-xl bg-primary-700 px-4 text-[13px] font-bold text-white transition hover:bg-primary-800 focus-visible:outline-2 focus-visible:outline-primary-500 sm:inline-flex">{t('landing.getStarted')}</Link>
                        <button type="button" className="grid size-10 place-items-center rounded-lg border border-slate-200 bg-white text-lg focus-visible:outline-2 focus-visible:outline-primary-500 dark:border-white/20 dark:bg-[#132434] lg:hidden" aria-label={t('landing.menuLabel')} aria-expanded={menuOpen} aria-controls="landing-mobile-nav" onClick={() => setMenuOpen(!menuOpen)}>{menuOpen ? '✕' : '☰'}</button>
                    </div>
                </div>
                {menuOpen && (
                    <nav id="landing-mobile-nav" className="grid gap-1 border-t border-slate-200 bg-white px-5 py-4 text-sm font-semibold dark:border-white/10 dark:bg-[#102332] lg:hidden" aria-label={t('landing.mobileNavigation')}>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="#why-averio" onClick={closeMenu}>{t('landing.whyNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="#features" onClick={closeMenu}>{t('landing.featuresNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="#how-it-works" onClick={closeMenu}>{t('landing.howNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="#faq" onClick={closeMenu}>{t('landing.faqNav')}</a>
                        <Link to="/login" search={{ redirect: undefined }} onClick={closeMenu} className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10">{t('landing.signIn')}</Link>
                        <Link to="/register" onClick={closeMenu} className="mt-1 rounded-xl bg-primary-700 px-4 py-3 text-center text-white">{t('landing.getStarted')}</Link>
                    </nav>
                )}
            </header>

            <main id="main-content">
                <section id="top" className="relative isolate scroll-mt-24 overflow-hidden py-16 sm:py-20 lg:py-28">
                    <div aria-hidden="true" className="pointer-events-none absolute -right-48 top-0 size-[620px] rounded-full bg-primary-500/[0.08] blur-[110px]" />
                    <div className="relative mx-auto grid max-w-[1250px] items-center gap-14 px-5 sm:px-8 lg:grid-cols-[1fr_1.05fr] lg:gap-10 lg:px-10">
                        <div className="max-w-[620px]">
                            <span className="inline-flex rounded-full border border-primary-200 bg-primary-50 px-4 py-2 text-[11px] font-bold uppercase tracking-[0.14em] text-primary-700 dark:border-primary-500/25 dark:bg-primary-500/10 dark:text-primary-300">{t('landing.heroEyebrow')}</span>
                            <h1 className="mt-7 text-[clamp(3.2rem,5.1vw,5.1rem)] font-extrabold leading-[1.06] tracking-[-0.065em] text-[#142b38] dark:text-white">{t('landing.heroLineOne')}<br /><span className="text-primary-600 dark:text-primary-400">{t('landing.heroLineTwo')}</span><br />{t('landing.heroLineThree')}</h1>
                            <p className="mt-7 max-w-[510px] text-[16px] leading-8 text-slate-600 dark:text-slate-300 sm:text-[18px]">{t('landing.heroDescription')}</p>
                            <div className="mt-9 flex flex-wrap gap-3"><Link to="/register" className={primaryLink}>{t('landing.startFree')} <span aria-hidden="true">↗</span></Link><a href="#features" className={secondaryLink}>{t('landing.exploreFeatures')} <span aria-hidden="true">↓</span></a></div>
                            <div className="mt-8 flex flex-wrap gap-5 text-[12px] font-semibold text-slate-500 dark:text-slate-400"><span>✓ {t('landing.soloFriendly')}</span><span>✓ {t('landing.privacyBuiltIn')}</span><span>✓ {t('landing.mobileReady')}</span></div>
                        </div>
                        <LandingPreview />
                    </div>
                </section>

                <section id="why-averio" className="scroll-mt-24 border-y border-[#e3ebe7] bg-white/75 py-14 dark:border-white/10 dark:bg-[#0d1b2a]">
                    <div className="mx-auto grid max-w-[1250px] items-center gap-10 px-5 sm:px-8 lg:grid-cols-[1fr_1.7fr] lg:px-10">
                        <div><p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-600 dark:text-primary-400">{t('landing.whyEyebrow')}</p><h2 className="mt-3 text-[27px] font-extrabold leading-tight tracking-tight">{t('landing.whyTitle')}</h2></div>
                        <div className="grid gap-6 sm:grid-cols-3">
                            {(['privacy', 'together', 'clarity'] as const).map((key, i) => <div key={key}><span className="flex size-10 items-center justify-center rounded-xl bg-primary-50 text-lg font-bold text-primary-700 dark:bg-primary-500/15 dark:text-primary-300">{['♙', '♧', '◉'][i]}</span><h3 className="mt-3 text-sm font-bold">{t('landing.' + key + 'Pillar')}</h3><p className={'mt-1 text-[12px] leading-6 ' + bodyText}>{t('landing.' + key + 'PillarDescription')}</p></div>)}
                        </div>
                    </div>
                </section>

                <section id="features" className="scroll-mt-24 py-24 sm:py-28">
                    <div className="mx-auto max-w-[1250px] px-5 sm:px-8 lg:px-10">
                        <p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-600 dark:text-primary-400">{t('landing.featuresEyebrow')}</p>
                        <h2 className="mt-4 max-w-[720px] text-[clamp(2.4rem,4.4vw,3.7rem)] font-extrabold leading-[1.13] tracking-[-0.055em]">{t('landing.featuresTitle')}</h2>
                        <p className={'mt-5 max-w-[690px] text-base leading-8 ' + bodyText}>{t('landing.featuresIntro')}</p>
                        <div className="mt-12 grid gap-5 lg:grid-cols-3">
                            {featureCards.map((card) => <article key={card.title} className={surface + ' relative overflow-hidden border-l-[3px] border-l-primary-500 p-7 sm:p-8'}><span className="flex size-12 items-center justify-center rounded-2xl bg-primary-100 text-[27px] font-bold text-primary-700 dark:bg-primary-500/15 dark:text-primary-200">{card.icon}</span><h3 className="mt-6 text-[21px] font-extrabold tracking-tight">{t('landing.' + card.title)}</h3><p className={'mt-3 min-h-[85px] text-sm leading-7 ' + bodyText}>{t('landing.' + card.description)}</p><div className="mt-5 rounded-2xl bg-[#f0f7f3] p-5 dark:bg-white/5"><span className="text-xs font-bold text-primary-700 dark:text-primary-300">✓ {t('landing.' + card.detail)}</span></div></article>)}
                        </div>
                    </div>
                </section>

                <section className="relative isolate overflow-hidden bg-[#0b1d2a] py-24 text-white sm:py-28">
                    <div aria-hidden="true" className="pointer-events-none absolute -right-40 -top-40 size-[600px] rounded-full bg-primary-500/10 blur-[110px]" />
                    <div className="relative mx-auto grid max-w-[1250px] items-center gap-12 px-5 sm:px-8 lg:grid-cols-2 lg:gap-20 lg:px-10">
                        <div><p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-300">{t('landing.privacyEyebrow')}</p><h2 className="mt-4 text-[clamp(2.4rem,4.5vw,3.8rem)] font-extrabold leading-[1.15] tracking-[-0.055em]">{t('landing.privacyTitle')}</h2><p className="mt-6 max-w-xl text-base leading-8 text-slate-300">{t('landing.privacyDescription')}</p><Link to="/register" className={primaryLink + ' mt-8'}>{t('landing.getStarted')} ↗</Link></div>
                        <div className="space-y-4">
                            <div className="rounded-2xl border border-white/10 bg-white/5 p-6"><div className="flex items-center justify-between gap-3"><strong>{t('landing.sharedCard')}</strong><span className="rounded-full bg-primary-400/15 px-3 py-1 text-xs text-primary-200">{t('landing.visibleTogether')}</span></div><p className="mt-3 text-sm leading-7 text-slate-300">{t('landing.sharedCardDetail')}</p></div>
                            <div className="ml-4 rounded-2xl border border-white/10 bg-white/5 p-6 sm:ml-10"><div className="flex items-center justify-between gap-3"><strong>{t('landing.privateCard')}</strong><span className="rounded-full bg-indigo-400/15 px-3 py-1 text-xs text-indigo-200">{t('landing.onlyYou')}</span></div><p className="mt-3 text-sm leading-7 text-slate-300">{t('landing.privateCardDetail')}</p></div>
                        </div>
                    </div>
                </section>

                <section className="py-24 sm:py-28"><div className="mx-auto max-w-[1250px] px-5 sm:px-8 lg:px-10"><p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-600 dark:text-primary-400">{t('landing.toolsEyebrow')}</p><h2 className="mt-4 max-w-[780px] text-[clamp(2.2rem,4vw,3.2rem)] font-extrabold leading-tight tracking-tight">{t('landing.toolsTitle')}</h2><div className="mt-10 grid gap-4 sm:grid-cols-2 lg:grid-cols-3">{capabilities.map(item => <article key={item.title} className={surface + ' border-l-[3px] border-l-primary-500 p-7'}><span className="flex size-11 items-center justify-center rounded-xl bg-primary-50 text-2xl font-bold text-primary-700 dark:bg-primary-500/15 dark:text-primary-200">{item.icon}</span><h3 className="mt-5 text-lg font-extrabold">{t('landing.' + item.title)}</h3><p className={'mt-3 text-sm leading-7 ' + bodyText}>{t('landing.' + item.text)}</p></article>)}</div></div></section>

                <section id="how-it-works" className="scroll-mt-24 bg-[#eaf5ee] py-24 dark:bg-[#102632]"><div className="mx-auto max-w-[1250px] px-5 sm:px-8 lg:px-10"><div className="mx-auto max-w-[730px] text-center"><p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-600 dark:text-primary-400">{t('landing.stepsEyebrow')}</p><h2 className="mt-4 text-[clamp(2.3rem,4vw,3.4rem)] font-extrabold leading-tight tracking-tight">{t('landing.stepsTitle')}</h2><p className={'mt-5 text-base leading-8 ' + bodyText}>{t('landing.stepsIntro')}</p></div><div className="mt-12 grid gap-5 md:grid-cols-3">{steps.map((step, i) => <article key={step} className={surface + ' p-7'}><span className="flex size-13 items-center justify-center rounded-2xl bg-primary-100 text-lg font-extrabold text-primary-700 dark:bg-primary-500/20 dark:text-primary-200">0{i + 1}</span><h3 className="mt-6 text-xl font-bold">{t('landing.' + step + 'Title')}</h3><p className={'mt-3 text-sm leading-7 ' + bodyText}>{t('landing.' + step + 'Description')}</p></article>)}</div><div className="mt-10 text-center"><Link to="/register" className={primaryLink}>{t('landing.createAccount')} →</Link></div></div></section>

                <section id="faq" className="scroll-mt-24 py-24 sm:py-28"><div className="mx-auto grid max-w-[1130px] gap-12 px-5 sm:px-8 lg:grid-cols-[0.8fr_1.2fr] lg:px-10"><div><p className="text-xs font-bold uppercase tracking-[0.18em] text-primary-600 dark:text-primary-400">{t('landing.faqEyebrow')}</p><h2 className="mt-4 text-[clamp(2.3rem,4vw,3.3rem)] font-extrabold leading-tight tracking-tight">{t('landing.faqTitle')}</h2><p className={'mt-5 text-sm leading-7 ' + bodyText}>{t('landing.faqIntro')}</p><a href="mailto:support@averioapp.com" className="mt-6 inline-block text-sm font-bold text-primary-700 hover:underline dark:text-primary-300">{t('landing.contactSupport')} ↗</a></div><div className="space-y-3">{questions.map(question => <details key={question} className={surface + ' group p-5'}><summary className="flex cursor-pointer list-none items-center justify-between gap-4 text-sm font-bold focus-visible:outline-2 focus-visible:outline-primary-500 [&::-webkit-details-marker]:hidden">{t('landing.faq_' + question + '_q')}<span aria-hidden="true" className="shrink-0 text-xl text-primary-600 group-open:rotate-45">+</span></summary><p className={'mt-3 text-sm leading-7 ' + bodyText}>{t('landing.faq_' + question + '_a')}</p></details>)}</div></div></section>

                <section className="px-5 pb-16 sm:px-8 lg:px-10"><div className="relative mx-auto max-w-[1170px] overflow-hidden rounded-[32px] bg-[#0b2530] px-6 py-20 text-center text-white sm:px-12 sm:py-24"><div aria-hidden="true" className="pointer-events-none absolute -right-32 -top-32 size-[400px] rounded-full bg-primary-500/20 blur-[100px]" /><div className="relative"><p className="text-xs font-bold uppercase tracking-[0.16em] text-primary-300">{t('landing.bottomEyebrow')}</p><h2 className="mx-auto mt-6 max-w-[770px] text-[clamp(2.5rem,5vw,4.5rem)] font-extrabold leading-[1.12] tracking-[-0.06em]">{t('landing.bottomTitle')}</h2><p className="mx-auto mt-6 max-w-[600px] text-base leading-8 text-slate-300">{t('landing.bottomDescription')}</p><div className="mt-9 flex flex-wrap justify-center gap-3"><Link to="/register" className={primaryLink}>{t('landing.getStarted')} ↗</Link><Link to="/login" search={{ redirect: undefined }} className="inline-flex min-h-12 items-center rounded-xl border border-white/25 px-6 py-3 text-sm font-semibold text-white hover:bg-white/10 focus-visible:outline-2 focus-visible:outline-white">{t('landing.signIn')}</Link></div></div></div></section>
            </main>

            <footer className="mx-auto flex max-w-[1250px] flex-col items-start justify-between gap-6 px-5 pb-10 sm:px-8 md:flex-row md:items-center lg:px-10"><AverioBrand /><div className="flex flex-wrap gap-6 text-xs font-semibold text-slate-500 dark:text-slate-400"><a href="#features" className="hover:text-primary-600">{t('landing.featuresNav')}</a><a href="#faq" className="hover:text-primary-600">{t('landing.faqNav')}</a><a href="mailto:support@averioapp.com" className="hover:text-primary-600">{t('landing.contact')}</a><span>© {new Date().getFullYear()} Averio</span></div></footer>
        </div>
    );
}


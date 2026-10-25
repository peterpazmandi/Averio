import { useState } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioBrand } from '../../shared/components/branding/AverioBrand';
import { ConceptPreferences } from '../../shared/components/concept/ConceptPreferences';

export function PublicHeader() {
    const { t } = useTranslation();
    const [menuOpen, setMenuOpen] = useState(false);
    function closeMenu() { setMenuOpen(false); }
    return (
            <header className="sticky top-0 z-50 border-b border-[#e3ebe7]/90 bg-[#f7faf8]/95 backdrop-blur-xl dark:border-white/10 dark:bg-[#07101f]/95">
                <div className="mx-auto flex min-h-[76px] max-w-[1250px] items-center justify-between gap-2 px-4 sm:px-8 lg:px-10">
                    <a href="/#top" className="shrink-0 rounded-lg focus-visible:outline-2 focus-visible:outline-primary-500" onClick={closeMenu} aria-label={t('landing.homeLabel')}><AverioBrand className="[&>span]:hidden min-[380px]:[&>span]:inline" /></a>
                    <nav className="hidden items-center gap-4 text-[13px] font-semibold text-slate-600 dark:text-slate-300 xl:flex" aria-label={t('landing.mainNavigation')}>
                        <a className="hover:text-primary-600" href="/#why-averio">{t('landing.whyNav')}</a>
                        <a className="hover:text-primary-600" href="/#features">{t('landing.featuresNav')}</a>
                        <a className="hover:text-primary-600" href="/#how-it-works">{t('landing.howNav')}</a>
                        <a className="hover:text-primary-600" href="/#faq">{t('landing.faqNav')}</a><Link to="/contact" onClick={closeMenu} className="rounded-lg p-2 hover:text-primary-600">{t('landing.contact')}</Link>
                    </nav>
                    <div className="ml-auto flex items-center gap-2">
                        <ConceptPreferences />
                        <Link to="/login" search={{ redirect: undefined }} className="hidden px-3 text-[13px] font-bold hover:text-primary-600 sm:block">{t('landing.signIn')}</Link>
                        <Link to="/register" className="hidden min-h-10 items-center rounded-xl bg-primary-700 px-4 text-[13px] font-bold text-white transition hover:bg-primary-800 focus-visible:outline-2 focus-visible:outline-primary-500 sm:inline-flex">{t('landing.getStarted')}</Link>
                        <button type="button" className="grid size-10 place-items-center rounded-lg border border-slate-200 bg-white text-lg focus-visible:outline-2 focus-visible:outline-primary-500 dark:border-white/20 dark:bg-[#132434] xl:hidden" aria-label={t('landing.menuLabel')} aria-expanded={menuOpen} aria-controls="landing-mobile-nav" onClick={() => setMenuOpen(!menuOpen)}>{menuOpen ? '✕' : '☰'}</button>
                    </div>
                </div>
                {menuOpen && (
                    <nav id="landing-mobile-nav" className="grid max-h-[calc(100dvh-77px)] overflow-y-auto overscroll-contain gap-1 border-t border-slate-200 bg-white px-5 py-4 text-sm font-semibold dark:border-white/10 dark:bg-[#102332] xl:hidden" aria-label={t('landing.mobileNavigation')}>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="/#why-averio" onClick={closeMenu}>{t('landing.whyNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="/#features" onClick={closeMenu}>{t('landing.featuresNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="/#how-it-works" onClick={closeMenu}>{t('landing.howNav')}</a>
                        <a className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10" href="/#faq" onClick={closeMenu}>{t('landing.faqNav')}</a><Link to="/contact" onClick={closeMenu} className="rounded-lg p-2 hover:text-primary-600">{t('landing.contact')}</Link>
                        <Link to="/login" search={{ redirect: undefined }} onClick={closeMenu} className="rounded-lg p-2 hover:bg-primary-50 dark:hover:bg-white/10">{t('landing.signIn')}</Link>
                        <Link to="/register" onClick={closeMenu} className="mt-1 rounded-xl bg-primary-700 px-4 py-3 text-center text-white">{t('landing.getStarted')}</Link>
                    </nav>
                )}
            </header>
    );
}
export function PublicFooter() {
    const { t } = useTranslation();
    return (
            <footer className="mx-auto flex max-w-[1250px] flex-col items-start justify-between gap-6 px-5 pb-10 sm:px-8 md:flex-row md:items-center lg:px-10"><AverioBrand /><div className="flex flex-wrap gap-6 text-xs font-semibold text-slate-500 dark:text-slate-400"><a href="/#features" className="hover:text-primary-600">{t('landing.featuresNav')}</a><a href="/#faq" className="hover:text-primary-600">{t('landing.faqNav')}</a><Link to="/contact" className="hover:text-primary-600">{t('landing.contact')}</Link><span>© {new Date().getFullYear()} Averio</span></div></footer>);
}

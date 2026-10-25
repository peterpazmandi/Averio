import { useEffect } from 'react';
import { Link } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { PublicHeader, PublicFooter } from './PublicLayout';

export function ContactPage() {
    const { t } = useTranslation();
    useEffect(() => {
        const previous = document.title;
        document.title = t('landing.contactDocumentTitle');
        return () => { document.title = previous; };
    }, [t]);
    return (
        <div className="min-h-screen bg-[#f7faf8] text-[#152033] dark:bg-[#07101f] dark:text-[#F3F6FA]">
            <a href="#main-content" className="sr-only rounded-lg bg-white p-3 text-primary-700 focus:not-sr-only focus:fixed focus:left-3 focus:top-3 focus:z-[100]">{t('landing.skipContent')}</a>
            <PublicHeader />
            <main id="main-content" className="mx-auto max-w-5xl px-5 py-14 sm:px-8 sm:py-20">
                <p className="text-xs font-bold uppercase tracking-widest text-primary-700 dark:text-primary-300">{t('landing.contact')}</p>
                <h1 className="mt-4 text-balance text-4xl font-extrabold tracking-tight sm:text-5xl">{t('landing.contactTitle')}</h1>
                <p className="mt-5 max-w-2xl text-base leading-8 text-slate-600 dark:text-slate-300">{t('landing.contactIntro')}</p>
                <div className="mt-10 grid gap-6 md:grid-cols-2">
                    <AverioCard className="p-6 sm:p-8">
                        <h2 className="text-xl font-bold">{t('landing.contactEmailTitle')}</h2>
                        <p className="mt-4 text-sm leading-7 text-slate-600 dark:text-slate-300">{t('landing.contactEmailHelp')}</p>
                        <a href="mailto:support@averioapp.com" className="mt-6 inline-flex min-h-11 max-w-full items-center break-all rounded-lg text-base font-bold text-primary-700 underline underline-offset-4 focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-primary-500 dark:text-primary-300">support@averioapp.com</a>
                    </AverioCard>
                    <AverioCard className="p-6 sm:p-8">
                        <h2 className="text-xl font-bold">{t('landing.contactDetailsTitle')}</h2>
                        <p className="mt-4 text-sm leading-7 text-slate-600 dark:text-slate-300">{t('landing.contactDetails')}</p>
                        <p className="mt-4 text-sm leading-7 text-slate-600 dark:text-slate-300">{t('landing.contactPrivacy')}</p>
                    </AverioCard>
                </div>
                <section className="my-10 border-t border-slate-200 pt-8 dark:border-slate-800">
                    <h2 className="text-xl font-bold">{t('landing.contactFaq')}</h2>
                    <p className="mt-3 text-sm leading-7 text-slate-600 dark:text-slate-300">{t('landing.contactFaqHelp')}</p>
                    <div className="mt-5 flex flex-wrap gap-6 font-semibold text-primary-700 dark:text-primary-300">
                        <Link to="/" hash="faq" className="underline underline-offset-4">{t('landing.contactFaqLink')}</Link>
                        <Link to="/" className="underline underline-offset-4">{t('landing.contactBack')}</Link>
                    </div>
                </section>
            </main>
            <PublicFooter />
        </div>
    );
}

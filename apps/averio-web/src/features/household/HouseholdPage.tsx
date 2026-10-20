import { Activity, useState } from 'react';
import { useLocation, useNavigate } from '@tanstack/react-router';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { HouseholdResponse } from '../../shared/api/generated';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioSectionSelect } from '../../shared/components/ui/AverioSectionSelect';
import { ConceptIcon } from '../../shared/components/concept/ConceptIcon';
import { householdQueries } from './householdQueries';
import { HouseholdEditor } from './HouseholdEditor';
import { PreviousHouseholds } from './PreviousHouseholds';
import { HouseholdInvitations } from './HouseholdInvitations';
import { PersonalMoneyPanel } from '../personal-money/PersonalMoneyPanel';

type Section = 'profile' | 'members' | 'invitations' | 'personalMoney' | 'privacy';
const decodeSection = (hash: string): Section => {
    switch (hash) {
        case '#household-members': return 'members';
        case '#household-invitations': return 'invitations';
        case '#household-personal-money': return 'personalMoney';
        case '#household-privacy': return 'privacy';
        default: return 'profile';
    }
};

export function HouseholdPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t, i18n } = useTranslation();
    const query = useQuery(householdQueries.detail(userId, householdId));
    const navigate = useNavigate();
    const hash = useLocation({ select: location => location.hash });
    const activeSection = decodeSection('#' + hash);
    const selectSection = (section: Section) => {
        // Section changes update history without moving the navigation out of view.
        void navigate({
            hash: 'household-' + (section === 'personalMoney' ? 'personal-money' : section),
            resetScroll: false,
            hashScrollIntoView: false,
        });
    };
    const [draft, setDraft] = useState<HouseholdResponse | null>(null);
    const [saved, setSaved] = useState(false);
    const [reloadRequired, setReloadRequired] = useState(false);
    async function reloadLatest() {
        setReloadRequired(true);
        setDraft(null);
        const result = await query.refetch();
        // A failed refresh retains cached data. Do not turn it into a new draft.
        if (result.isSuccess) setReloadRequired(false);
    }
    const retry = () => { void (reloadRequired ? reloadLatest() : query.refetch()); };
    const language = i18n.resolvedLanguage ?? i18n.language;
    const muted = 'text-sm text-slate-600 dark:text-slate-300';

    if (query.isPending) return <p role="status">{t('common.status.loading')}</p>;
    if (!query.data) return <AverioCard className="space-y-4 p-5">
        <p role="alert">{t('householdSettings.loadError')}</p>
        <AverioButton disabled={query.isFetching} onClick={retry}>{t('common.actions.retry')}</AverioButton>
    </AverioCard>;
    const household = query.data;
    const owner = household.role === 'owner';
    const visibleSection = !owner && activeSection === 'invitations' ? 'profile' : activeSection;
    const sections: Section[] = owner
        ? ['profile', 'members', 'invitations', 'personalMoney', 'privacy']
        : ['profile', 'members', 'personalMoney', 'privacy'];
    const menuIcons = { profile: 'household', members: 'group', invitations: 'receipt', personalMoney: 'savings', privacy: 'lock' } as const;
    const countryName = household.countryCode
        ? new Intl.DisplayNames([language], { type: 'region' }).of(household.countryCode) ?? household.countryCode
        : t('householdSettings.notSet');

    return <div className="space-y-6 text-[#152033] dark:text-[#F3F6FA]">
        <header className="space-y-2">
            <h2 className="text-2xl font-bold tracking-tight">{t('householdSettings.title')}</h2>
            <p className={muted}>{t('householdSettings.description')}</p>
        </header>
        {query.isError && <div role="alert" className="flex flex-wrap items-center gap-3 text-sm text-red-700 dark:text-red-300">
            <p>{t('householdSettings.loadError')}</p>
            <AverioButton disabled={query.isFetching} onClick={retry}>{t('common.actions.retry')}</AverioButton>
        </div>}
        <div className="grid items-start gap-5 lg:grid-cols-[240px_minmax(0,1fr)]">
            <AverioCard role="navigation" aria-label={t('householdSettings.sections')}
                className="p-3 lg:sticky lg:top-24">
                <div className="lg:hidden">
                    <AverioSectionSelect label={t('householdSettings.sections')} value={visibleSection}
                        items={sections.map(section => ({ value: section, label: t('householdSettings.compact.' + section), icon: menuIcons[section] }))}
                        onChange={selectSection} />
                </div>
                <div className="hidden gap-2 lg:grid">
                {sections.map(section => {
                    const selected = visibleSection === section;
                    const href = '#household-' + (section === 'personalMoney' ? 'personal-money' : section);
                    return <a key={section} href={href} aria-current={selected ? 'page' : undefined}
                        onClick={event => {
                            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
                            event.preventDefault();
                            selectSection(section);
                        }}
                        className={`group flex min-h-12 min-w-0 w-full items-center gap-3 rounded-xl border-l-[3px] px-3 py-3 text-sm transition-colors focus-visible:outline-2 focus-visible:outline-primary-500 ${selected
                            ? 'border-l-emerald-500 bg-emerald-100 font-bold text-emerald-900 shadow-sm ring-1 ring-emerald-300/60 dark:bg-emerald-950 dark:text-emerald-100 dark:ring-emerald-800'
                            : 'border-l-transparent font-medium hover:bg-emerald-50 dark:hover:bg-emerald-900/30'}`}>
                        <span className={`shrink-0 ${selected ? 'text-emerald-700 dark:text-emerald-300' : 'text-slate-500 dark:text-slate-400'}`}>
                            <ConceptIcon name={menuIcons[section]} />
                        </span>
                        <span className="min-w-0 text-left">
                            <span className="block wrap-normal">{t('householdSettings.' + section)}</span>
                            <span className="mt-0.5 hidden text-[11px] font-normal leading-snug text-slate-600 dark:text-slate-300 lg:block">
                                {t('householdSettings.navHelp.' + section)}
                            </span>
                        </span>
                    </a>;
                })}
                </div>
            </AverioCard>
            <div className="min-w-0 space-y-5">
                <Activity mode={visibleSection === 'profile' ? 'visible' : 'hidden'}>
                <AverioCard id="household-profile" className="scroll-mt-24 space-y-5 p-5 sm:p-6">
                    <div className="flex flex-wrap items-start justify-between gap-3">
                        <div><h3 className="text-lg font-semibold">{t('householdSettings.profile')}</h3><p className={muted}>{t('householdSettings.profileHelp')}</p></div>
                        {owner && !draft && <AverioButton disabled={reloadRequired || query.isFetching}
                            onClick={() => { if (reloadRequired || query.isFetching) return; setDraft(household); setSaved(false); }}>{t('householdSettings.edit')}</AverioButton>}
                    </div>
                    {saved && <p role="status" className="text-sm text-emerald-800 dark:text-emerald-200">{t('householdSettings.saved')}</p>}
                    {draft && owner ? <HouseholdEditor household={draft} userId={userId} onSaved={() => { setDraft(null); setSaved(true); }}
                        onCancel={() => { void reloadLatest(); }} /> : <dl className="grid gap-5 sm:grid-cols-2">
                        <div className="sm:col-span-2"><dt className={muted}>{t('householdSettings.name')}</dt><dd className="mt-1 break-words font-semibold">{household.name}</dd></div>
                        <div><dt className={muted}>{t('householdSettings.country')}</dt><dd className="mt-1 font-semibold">{countryName}</dd></div>
                        <div><dt className={muted}>{t('householdSettings.currency')}</dt><dd className="mt-1 font-semibold">{household.preferredCurrency}</dd><p className="mt-2 text-xs text-slate-600 dark:text-slate-300">{t('householdSettings.currencyHelp')}</p></div>
                    </dl>}
                    {!owner && <p className={muted}>{t('householdSettings.readOnly')}</p>}
                </AverioCard>
                </Activity>
                {visibleSection === 'members' && <AverioCard id="household-members" className="scroll-mt-24 space-y-4 p-5 sm:p-6">
                    <div className="flex flex-wrap items-start justify-between gap-2">
                        <div><h3 className="text-lg font-semibold">{t('householdSettings.members')}</h3><p className={muted}>{t('householdSettings.membersHelp')}</p></div>
                        <span className="rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100">{t('householdSettings.memberCount', { count: household.members.length })}</span>
                    </div>
                    <ul className="divide-y divide-emerald-900/10 dark:divide-emerald-100/10">
                        {household.members.map(member => <li key={member.id} className="flex items-start gap-3 py-4">
                            <span aria-hidden="true" className="flex size-10 shrink-0 items-center justify-center rounded-xl bg-emerald-100 text-emerald-800 dark:bg-emerald-900/50 dark:text-emerald-100"><ConceptIcon name="profile" /></span>
                            <div className="min-w-0 flex-1 space-y-1">
                                <p className="break-words font-semibold">{member.displayName || t('householdSettings.unnamedMember')} {member.isCurrentUser && <span className="text-xs font-normal">({t('householdSettings.you')})</span>}</p>
                                <p className="text-xs text-slate-600 dark:text-slate-300">{t('householdSettings.joined', { date: new Intl.DateTimeFormat(language, { dateStyle: 'medium', timeZone: 'UTC' }).format(new Date(member.joinedAtUtc)) })}</p>
                            </div>
                            <span className="rounded-full border border-emerald-900/15 px-2 py-1 text-xs font-semibold dark:border-emerald-100/20">{t(member.role === 'owner' ? 'householdSettings.owner' : 'householdSettings.member')}</span>
                        </li>)}
                    </ul>
                </AverioCard>}
                {visibleSection === 'members' && <PreviousHouseholds userId={userId} householdId={householdId} />}
                <Activity mode={visibleSection === 'personalMoney' ? 'visible' : 'hidden'}>
                    <PersonalMoneyPanel userId={userId} householdId={householdId} household={household} />
                </Activity>
                {owner && <Activity mode={visibleSection === 'invitations' ? 'visible' : 'hidden'}>
                    <section id="household-invitations" className="scroll-mt-24">
                        <HouseholdInvitations userId={userId} householdId={householdId} />
                    </section>
                </Activity>}
                {visibleSection === 'privacy' && <AverioCard id="household-privacy" className="scroll-mt-24 space-y-3 p-5 sm:p-6">
                    <h3 className="text-lg font-semibold">{t('householdSettings.privacy')}</h3>
                    <p className={muted}>{t('householdSettings.privacyHelp')}</p>
                    <p className={muted}>{t('householdSettings.balanceHelp')}</p>
                </AverioCard>}
            </div>
        </div>
    </div>;
}


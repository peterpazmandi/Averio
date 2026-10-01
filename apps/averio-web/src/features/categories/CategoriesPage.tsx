import { useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CategoryResponse, CreateCategoryRequest } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioFilterPanel } from '../../shared/components/ui/AverioFilterPanel';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { getCategoryDisplayName } from '../transactions/categoryLocalization';
import { CategoryEditor } from './CategoryEditor';
import { CategoryIllustration } from './CategoryIllustration';
import { categoryQueries, useCategoryMutations } from './categoryQueries';

export function CategoriesPage({ userId, householdId }: { userId: string; householdId: string }) {
    const { t } = useTranslation();
    const query = useQuery(categoryQueries.management(userId, householdId));
    const mutations = useCategoryMutations(userId, householdId);
    const [search, setSearch] = useState('');
    const [kind, setKind] = useState('');
    const [source, setSource] = useState('');
    const [status, setStatus] = useState('active');
    const [editor, setEditor] = useState<{ category: CategoryResponse | null } | null>(null);
    const [editorRevision, setEditorRevision] = useState(0);
    const [confirmation, setConfirmation] = useState<CategoryResponse | null>(null);
    const [archiveError, setArchiveError] = useState('');
    const [archiveConflict, setArchiveConflict] = useState(false);
    const [reloadBusy, setReloadBusy] = useState(false);
    const categories = query.data ?? [];
    const busy = mutations.create.isPending || mutations.update.isPending || mutations.archive.isPending;
    const count = Number(Boolean(search.trim())) + Number(Boolean(kind)) + Number(Boolean(source)) + Number(status !== 'active');
    const reset = () => { setSearch(''); setKind(''); setSource(''); setStatus('active'); };
    const matches = (c: CategoryResponse) => (!kind || c.kind === kind || c.kind === 'Both')
        && (!source || (source === 'system' ? c.isSystem : !c.isSystem))
        && (status === 'all' || c.isArchived === (status === 'archived'))
        && (!search.trim() || getCategoryDisplayName(c, t).toLocaleLowerCase().includes(search.trim().toLocaleLowerCase()));
    const groups = categories.filter(c => !c.parentId).map(parent => ({
        parent, parentMatches: matches(parent),
        children: categories.filter(c => c.parentId === parent.id && matches(c)),
    })).filter(g => g.parentMatches || g.children.length > 0);
    async function save(request: CreateCategoryRequest) {
        if (editor?.category) {
            await mutations.update.mutateAsync({ id: editor.category.id,
                request: { ...request, expectedUpdatedAt: editor.category.updatedAt } });
        } else await mutations.create.mutateAsync(request);
        setEditor(null);
    }
    async function reloadEditor() {
        const result = await query.refetch();
        if (result.isError) throw result.error;
        setEditorRevision(value => value + 1);
        if (!editor?.category) return;
        const latest = result.data?.find(c => c.id === editor.category?.id);
        setEditor(latest && !latest.isArchived && !latest.isSystem ? { category: latest } : null);
    }
    async function changeArchive() {
        if (!confirmation || busy || archiveConflict) return;
        setArchiveError('');
        try { await mutations.archive.mutateAsync(confirmation); setConfirmation(null); }
        catch (reason) {
            setArchiveConflict(reason instanceof ApiError && [403, 404, 409].includes(reason.status));
            setArchiveError(reason instanceof ApiError ? reason.message : t('errors.genericDescription'));
        }
    }
    async function reloadArchive() {
        setReloadBusy(true);
        try {
            const result = await query.refetch();
            if (result.isError) throw result.error;
            setConfirmation(null);
            setArchiveError('');
            setArchiveConflict(false);
        } catch { setArchiveError(t('errors.genericDescription')); }
        finally { setReloadBusy(false); }
    }
    function row(c: CategoryResponse, contextOnly = false) {
        return <div key={c.id} className={`flex min-w-0 flex-wrap items-center gap-3 py-3 ${c.parentId ? 'ml-5 border-l border-primary-500/30 pl-3' : ''}`}>
            <span className="flex size-11 shrink-0 items-center justify-center rounded-xl bg-white text-2xl dark:bg-[#0D1726]" aria-hidden="true">{c.icon}</span>
            <div className="min-w-0 flex-1">
                <h3 className="break-words font-semibold">{getCategoryDisplayName(c, t)}</h3>
                <p className="text-xs text-[#667085] dark:text-[#AAB4C4]">
                    {t('categoryManagement.' + c.kind)} · {t(c.isSystem ? 'categoryManagement.system' : 'categoryManagement.custom')}
                    {c.isArchived && <> · {t('categoryManagement.archived')}</>}
                    {contextOnly && <> · {t('categoryManagement.parentContext')}</>}
                </p>
            </div>
            {!contextOnly && !c.isSystem && <div className="flex flex-wrap gap-2">
                {!c.isArchived && <AverioButton disabled={busy} onClick={() => setEditor({ category: c })}
                    aria-label={t('categoryManagement.editNamed', { name: getCategoryDisplayName(c, t) })}>
                    {t('categoryManagement.edit')}
                </AverioButton>}
                <AverioButton disabled={busy} onClick={() => { setArchiveError(''); setArchiveConflict(false); setConfirmation(c); }}
                    aria-label={t(c.isArchived ? 'categoryManagement.restoreNamed' : 'categoryManagement.archiveNamed', { name: getCategoryDisplayName(c, t) })}>
                    {t(c.isArchived ? 'categoryManagement.restore' : 'categoryManagement.archive')}
                </AverioButton>
            </div>}
        </div>;
    }
    return <section className="grid min-w-0 gap-5" aria-labelledby="categories-title">
        <header className="flex flex-wrap items-start justify-between gap-4">
            <div>
                <h2 id="categories-title" className="text-2xl font-bold text-[#152033] dark:text-[#F3F6FA]">{t('categoryManagement.title')}</h2>
                <p className="mt-1 max-w-2xl text-sm text-[#667085] dark:text-[#AAB4C4]">{t('categoryManagement.description')}</p>
            </div>
            <AverioButton variant="primary" disabled={!query.isSuccess || busy} onClick={() => setEditor({ category: null })}>
                {t('categoryManagement.create')}
            </AverioButton>
        </header>
        <div className="flex flex-wrap items-center justify-between gap-3">
            <p className="text-sm text-[#667085] dark:text-[#AAB4C4]">{t('categoryManagement.systemHelp')}</p>
            <AverioFilterPanel count={count} onReset={reset}
                labels={{ filters: t('categoryManagement.filters'), showResults: t('categoryManagement.results'),
                    reset: t('categoryManagement.reset'), summary: t('categoryManagement.filters'), close: t('categoryManagement.cancel') }}
                summary={t('categoryManagement.activeFilters', { count })}>
                <div className="grid gap-4">
                    <AverioField>{t('categoryManagement.search')}
                        <AverioInput type="search" value={search} onChange={e => setSearch(e.target.value)} />
                    </AverioField>
                    <AverioField>{t('categoryManagement.kind')}
                        <AverioSelect value={kind} onChange={e => setKind(e.target.value)}>
                            <option value="">{t('categoryManagement.all')}</option>
                            {['Expense', 'Income'].map(k => <option key={k} value={k}>{t('categoryManagement.' + k)}</option>)}
                        </AverioSelect>
                    </AverioField>
                    <AverioField>{t('categoryManagement.source')}
                        <AverioSelect value={source} onChange={e => setSource(e.target.value)}>
                            <option value="">{t('categoryManagement.all')}</option>
                            <option value="system">{t('categoryManagement.system')}</option>
                            <option value="custom">{t('categoryManagement.custom')}</option>
                        </AverioSelect>
                    </AverioField>
                    <AverioField>{t('categoryManagement.status')}
                        <AverioSelect value={status} onChange={e => setStatus(e.target.value)}>
                            {['active', 'archived', 'all'].map(s => <option key={s} value={s}>{t('categoryManagement.' + s)}</option>)}
                        </AverioSelect>
                    </AverioField>
                </div>
            </AverioFilterPanel>
        </div>
        {query.isPending && <p role="status">{t('common.status.loading')}</p>}
        {query.isError && <AverioCard>
            <p role="alert">{t('errors.genericDescription')}</p>
            <AverioButton disabled={query.isFetching} onClick={() => void query.refetch()}>{t('common.actions.retry')}</AverioButton>
        </AverioCard>}
        {query.isSuccess && groups.length === 0 && <AverioCard className="py-10 text-center">
            <CategoryIllustration />
            <h3 className="text-lg font-bold">{t(count ? 'categoryManagement.noMatches' : 'categoryManagement.empty')}</h3>
            <p className="mx-auto my-3 max-w-md text-sm">{t(count ? 'categoryManagement.noMatchesHelp' : 'categoryManagement.emptyHelp')}</p>
            <AverioButton variant="primary" onClick={count ? reset : () => setEditor({ category: null })}>
                {t(count ? 'categoryManagement.reset' : 'categoryManagement.create')}
            </AverioButton>
        </AverioCard>}
        {query.isSuccess && groups.map(g => <AverioCard key={g.parent.id}>
            {row(g.parent, !g.parentMatches)}
            {g.children.length > 0 && <div className="border-t border-primary-500/15">{g.children.map(c => row(c))}</div>}
        </AverioCard>)}
        {editor && <CategoryEditor key={editorRevision + (editor.category ? editor.category.id + editor.category.updatedAt : 'new')}
            category={editor.category} categories={categories} busy={busy} onClose={() => setEditor(null)}
            onSave={save} onReload={reloadEditor} />}
        {confirmation && <AverioDialog open busy={busy || reloadBusy}
            title={t(confirmation.isArchived ? 'categoryManagement.restore' : 'categoryManagement.archive')}
            closeLabel={t('categoryManagement.cancel')} onDismiss={() => setConfirmation(null)}
            actions={<>
                <AverioButton disabled={busy || reloadBusy} onClick={() => setConfirmation(null)}>{t('categoryManagement.cancel')}</AverioButton>
                {archiveConflict && <AverioButton disabled={busy || reloadBusy} onClick={() => void reloadArchive()}>{t('categoryManagement.reload')}</AverioButton>}
                <AverioButton variant={confirmation.isArchived ? 'primary' : 'danger'} disabled={busy || reloadBusy || archiveConflict} onClick={() => void changeArchive()}>
                    {t(confirmation.isArchived ? 'categoryManagement.restore' : 'categoryManagement.archive')}
                </AverioButton>
            </>}>
            <AverioCard>
                <p className="font-semibold">{getCategoryDisplayName(confirmation, t)}</p>
                <p>{t(confirmation.isArchived ? 'categoryManagement.restoreHelp' : 'categoryManagement.archiveHelp')}</p>
            </AverioCard>
            {archiveError && <p role="alert" className="mt-3 text-red-700 dark:text-red-300">{archiveError}</p>}
        </AverioDialog>}
    </section>;
}

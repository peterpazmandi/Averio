import { descendantsOf } from './categoryTree';
import { useId, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import type { CategoryResponse, CreateCategoryRequest } from '../../shared/api/generated';
import { ApiError } from '../../shared/api/ApiError';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioDialog } from '../../shared/components/ui/AverioDialog';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { AverioIconPicker } from '../../shared/components/ui/AverioIconPicker';
import { AverioCategorySelect } from '../../shared/components/ui/AverioCategorySelect';
import { categoryQueries } from './categoryQueries';
import { InfoPopover } from '../../shared/components/ui/InfoPopover';

interface Props {
    category: CategoryResponse | null;
    categories: CategoryResponse[];
    busy: boolean;
    onClose: () => void;
    onSave: (request: CreateCategoryRequest) => Promise<void>;
    onReload: () => Promise<void>;
    transactionKind?: 'Expense' | 'Income';
}
export function CategoryEditor({ category, categories, busy, onClose, onSave, onReload, transactionKind }: Props) {
    const { t } = useTranslation();
    const formId = useId();
    const icons = useQuery(categoryQueries.icons());
    const [name, setName] = useState(category?.name ?? '');
    const [kind, setKind] = useState(category?.kind ?? transactionKind ?? 'Expense');
    const [parentId, setParentId] = useState(category?.parentId ?? '');
    const [icon, setIcon] = useState(category?.icon ?? '🛒');
    const [accentColor, setAccentColor] = useState(category?.accentColor ?? '#10B981');
    const [error, setError] = useState('');
    const [conflict, setConflict] = useState(false);
    const [reloadBusy, setReloadBusy] = useState(false);
    const excluded = category ? descendantsOf(category.id, categories) : new Set<string>();
    const parents = categories.filter(c => !c.isArchived && !excluded.has(c.id)
        && (c.kind === 'Both' || c.kind === kind));
    const invalidParent = Boolean(parentId && !parents.some(c => c.id === parentId));
    async function submit(event: React.SubmitEvent<HTMLFormElement>) {
        event.preventDefault();
        if (busy || conflict || invalidParent || !icons.isSuccess) return;
        setError('');
        try { await onSave({ name: name.trim(), kind, parentId: parentId || null, icon, accentColor }); }
        catch (reason) {
            setConflict(reason instanceof ApiError && [403, 404, 409].includes(reason.status));
            setError(reason instanceof ApiError
                ? Object.values(reason.validationErrors ?? {}).flat().join(' ') || reason.message
                : t('errors.genericDescription'));
        }
    }
    async function reload() {
        setReloadBusy(true);
        try { await onReload(); }
        catch { setError(t('errors.genericDescription')); }
        finally { setReloadBusy(false); }
    }
    return <AverioDialog open title={t(category ? 'categoryManagement.edit' : 'categoryManagement.create')}
        closeLabel={t('categoryManagement.cancel')} busy={busy || reloadBusy} onDismiss={onClose}
        actions={<>
            <AverioButton disabled={busy || reloadBusy} onClick={onClose}>{t('categoryManagement.cancel')}</AverioButton>
            {conflict && <AverioButton disabled={busy || reloadBusy} onClick={() => void reload()}>
                {t('categoryManagement.reload')}
            </AverioButton>}
            <AverioButton variant="primary" form={formId} type="submit"
                disabled={busy || reloadBusy || conflict || invalidParent || !icons.isSuccess}>
                {t('categoryManagement.save')}
            </AverioButton>
        </>}>
        <form id={formId} onSubmit={event => void submit(event)} className="grid gap-4">
            {error && <p role="alert" className="text-red-700 dark:text-red-300">{error}</p>}
            {conflict && <p>{t('categoryManagement.conflict')}</p>}
            <AverioCard className="grid gap-4">
                <h3 className="font-bold" aria-label={t('categoryManagement.details')}>{t('categoryManagement.details')} <InfoPopover label={t('categoryManagement.details')}>{t('categoryManagement.sharedHelp')}</InfoPopover></h3>
                <AverioField>{t('categoryManagement.name')}
                    <AverioInput value={name} required maxLength={100} disabled={busy}
                        onChange={e => setName(e.target.value)} />
                </AverioField>
                <div className="grid gap-[7px]">
                    <div className="text-[13px] font-semibold"><label htmlFor={formId + '-kind'}>{t('categoryManagement.kind')}</label>
                        {category && <InfoPopover label={t('categoryManagement.kind')}>{t('categoryManagement.kindFixed')}</InfoPopover>}</div>
                    <AverioSelect id={formId + '-kind'} value={kind} disabled={busy || Boolean(category)}
                        onChange={e => { setKind(e.target.value); setParentId(''); }}>
                        {(transactionKind ? [transactionKind, 'Both'] : ['Expense', 'Income', 'Both']).map(k => <option key={k} value={k}>{t('categoryManagement.' + k)}</option>)}
                    </AverioSelect>
                </div>
                <div className="grid gap-2">
                    <p className="text-[13px] font-semibold">{t('categoryManagement.parent')} <InfoPopover label={t('categoryManagement.parent')}>{t('categoryManagement.parentHelp')}</InfoPopover></p>
                    <AverioCategorySelect categories={parents} contextCategories={categories} value={parentId} onChange={setParentId}
                        disabled={busy} label={t('categoryManagement.parent')} noneLabel={t('categoryManagement.noParent')} />
                </div>
            </AverioCard>
            <AverioCard className="grid gap-4">
                <h3 className="font-bold">{t('categoryManagement.appearance')}</h3>
                {icons.isPending && <p role="status">{t('common.status.loading')}</p>}
                {icons.isError && <div role="alert">
                    <p>{t('errors.genericDescription')}</p>
                    <AverioButton onClick={() => void icons.refetch()}>{t('common.actions.retry')}</AverioButton>
                </div>}
                <AverioIconPicker icons={icons.data ?? []} value={icon} onChange={setIcon} disabled={busy || !icons.isSuccess} />
                <AverioField>{t('categoryManagement.color')}
                    <AverioInput type="color" value={accentColor} disabled={busy} onChange={e => setAccentColor(e.target.value)} />
                </AverioField>
                <div className="flex items-center gap-3 rounded-xl border border-primary-500/25 p-3">
                    <span className="text-3xl" aria-hidden="true">{icon}</span>
                    <svg className="size-3 shrink-0" viewBox="0 0 12 12" aria-hidden="true"><circle cx="6" cy="6" r="5" fill={accentColor} /></svg>
                    <span className="min-w-0 break-words font-semibold">{name || t('categoryManagement.preview')}</span>
                </div>
            </AverioCard>
        </form>
    </AverioDialog>;
}

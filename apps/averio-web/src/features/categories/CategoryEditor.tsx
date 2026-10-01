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

interface Props {
    category: CategoryResponse | null;
    categories: CategoryResponse[];
    busy: boolean;
    onClose: () => void;
    onSave: (request: CreateCategoryRequest) => Promise<void>;
    onReload: () => Promise<void>;
}
export function CategoryEditor({ category, categories, busy, onClose, onSave, onReload }: Props) {
    const { t } = useTranslation();
    const formId = useId();
    const icons = useQuery(categoryQueries.icons());
    const [name, setName] = useState(category?.name ?? '');
    const [kind, setKind] = useState(category?.kind ?? 'Expense');
    const [parentId, setParentId] = useState(category?.parentId ?? '');
    const [icon, setIcon] = useState(category?.icon ?? '🛒');
    const [accentColor, setAccentColor] = useState(category?.accentColor ?? '#10B981');
    const [error, setError] = useState('');
    const [conflict, setConflict] = useState(false);
    const [reloadBusy, setReloadBusy] = useState(false);
    const hasChildren = Boolean(category && categories.some(c => c.parentId === category.id));
    const parents = categories.filter(c => !c.parentId && !c.isArchived && c.id !== category?.id
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
                <h3 className="font-bold">{t('categoryManagement.details')}</h3>
                <AverioField>{t('categoryManagement.name')}
                    <AverioInput value={name} required maxLength={100} disabled={busy}
                        onChange={e => setName(e.target.value)} />
                </AverioField>
                <AverioField>{t('categoryManagement.kind')}
                    <AverioSelect value={kind} disabled={busy || Boolean(category)}
                        onChange={e => { setKind(e.target.value); setParentId(''); }}>
                        {['Expense', 'Income', 'Both'].map(k => <option key={k} value={k}>{t('categoryManagement.' + k)}</option>)}
                    </AverioSelect>
                </AverioField>
                {category && <p className="text-xs">{t('categoryManagement.kindFixed')}</p>}
                <div className="grid gap-2">
                    <p className="text-[13px] font-semibold">{t('categoryManagement.parent')}</p>
                    <AverioCategorySelect categories={parents} value={parentId} onChange={setParentId}
                        disabled={busy || hasChildren} label={t('categoryManagement.parent')} noneLabel={t('categoryManagement.noParent')} />
                </div>
                <p className="text-xs">{t(hasChildren ? 'categoryManagement.childrenHelp' : 'categoryManagement.parentHelp')}</p>
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
            <p className="text-xs">{t('categoryManagement.sharedHelp')}</p>
        </form>
    </AverioDialog>;
}

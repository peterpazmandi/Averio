import { useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { createPasskey, passkeyApi, passkeysSupported } from '../authentication/api/passkeyApi';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput } from '../../shared/components/ui/AverioFormControls';
import { PasswordField } from '../../shared/components/PasswordField';
import { profileKeys } from './profileQueries';

export function PasskeySettings({ userId }: { userId: string }) {
    const { t, i18n } = useTranslation();
    const client = useQueryClient();
    const keys = useQuery({ queryKey: profileKeys.passkeys(userId), queryFn: ({ signal }) => passkeyApi.list(signal) });
    const [name, setName] = useState('');
    const [password, setPassword] = useState('');
    const [removeId, setRemoveId] = useState<string>();
    const [removePassword, setRemovePassword] = useState('');
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');

    const add = useMutation({
        mutationFn: () => createPasskey(password, name.trim()),
        onSuccess: async () => {
            setPassword(''); setName(''); setError(''); setMessage(t('profile.passkeyAdded'));
            await client.invalidateQueries({ queryKey: profileKeys.passkeys(userId) });
        },
        onError: (reason: unknown) => {
            if (reason instanceof DOMException && reason.name === 'NotAllowedError') return;
            setError(t('profile.passkeyFailed'));
        },
    });
    const remove = useMutation({
        mutationFn: () => passkeyApi.remove(removeId ?? '', removePassword),
        onSuccess: async () => {
            setRemovePassword(''); setRemoveId(undefined); setError(''); setMessage(t('profile.passkeyRemoved'));
            await client.invalidateQueries({ queryKey: profileKeys.passkeys(userId) });
        },
        onError: () => setError(t('profile.passkeyRemoveFailed')),
    });

    function submit(event: FormEvent<HTMLFormElement>) {
        event.preventDefault(); setError(''); setMessage(''); add.mutate();
    }

    return <AverioCard id="profile-passkeys" className="scroll-mt-24">
        <h3 className="text-lg font-semibold">{t('profile.passkeys')}</h3>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-300">{t('profile.passkeyHelp')}</p>
        <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">{t('profile.passkeyMfaHelp')}</p>

        {keys.isPending && <p role="status" className="mt-5 text-sm">{t('common.status.loading')}</p>}
        {keys.isError && <p role="alert" className="mt-5 text-sm text-red-700 dark:text-red-300">{t('profile.passkeyLoadFailed')}</p>}
        {keys.data && <div className="mt-5 space-y-3">
            {keys.data.length === 0 && <p className="rounded-xl bg-emerald-50 p-4 text-sm text-slate-700 dark:bg-emerald-950/30 dark:text-slate-200">{t('profile.passkeyEmpty')}</p>}
            {keys.data.map(key => <div key={key.credentialId} className="rounded-xl border-l-[3px] border-emerald-500 bg-slate-50 p-4 dark:bg-slate-900/60">
                <div className="flex flex-wrap items-center justify-between gap-3">
                    <div><p className="font-semibold">{key.name}</p><p className="text-xs text-slate-500 dark:text-slate-400">{t('profile.passkeyCreated', { date: new Intl.DateTimeFormat(i18n.language, { dateStyle: 'medium' }).format(new Date(key.createdAt)) })}</p></div>
                    <AverioButton disabled={remove.isPending} onClick={() => { setRemoveId(key.credentialId); setRemovePassword(''); setError(''); }}>{t('profile.passkeyRemove')}</AverioButton>
                </div>
                {removeId === key.credentialId && <form onSubmit={event => { event.preventDefault(); remove.mutate(); }} className="mt-4 max-w-md space-y-3">
                    <p className="text-sm">{t('profile.passkeyRemoveHelp')}</p>
                    <PasswordField label={t('profile.passwordConfirm')} autoComplete="current-password" required value={removePassword} onChange={event => setRemovePassword(event.target.value)} />
                    <div className="flex gap-2"><AverioButton type="submit" variant="danger" disabled={remove.isPending}>{t('profile.passkeyRemove')}</AverioButton><AverioButton onClick={() => setRemoveId(undefined)}>{t('profile.cancel')}</AverioButton></div>
                </form>}
            </div>)}
        </div>}

        {passkeysSupported() ? <form onSubmit={submit} className="mt-6 max-w-lg space-y-4 border-t border-slate-200 pt-5 dark:border-slate-700">
            <h4 className="font-semibold">{t('profile.passkeyAdd')}</h4>
            <AverioField>{t('profile.passkeyName')}<AverioInput required maxLength={64} value={name} placeholder={t('profile.passkeyNameExample')} onChange={event => setName(event.target.value)} /></AverioField>
            <PasswordField label={t('profile.passwordConfirm')} autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} />
            <AverioButton type="submit" variant="primary" disabled={add.isPending || (keys.data?.length ?? 0) >= 10}>{t('profile.passkeyAdd')}</AverioButton>
        </form> : <p className="mt-5 text-sm text-amber-800 dark:text-amber-300">{t('profile.passkeyUnsupported')}</p>}
        {(message || error) && <p role={error ? 'alert' : 'status'} className={`mt-4 text-sm ${error ? 'text-red-700 dark:text-red-300' : 'text-emerald-700 dark:text-emerald-300'}`}>{error || message}</p>}
    </AverioCard>;
}

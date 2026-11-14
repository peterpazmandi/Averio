import { useEffect, useRef, useState, type FormEvent } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../authentication/context/AuthProvider';
import { AverioButton } from '../../shared/components/ui/AverioButton';
import { AverioCard } from '../../shared/components/ui/AverioCard';
import { AverioField, AverioInput, AverioSelect } from '../../shared/components/ui/AverioFormControls';
import { profileApi, type UserProfile } from './profileApi';
import { profileKeys, profileQueries } from './profileQueries';
import { ProfileAvatar } from './ProfileAvatar';

export function ProfilePage({ userId }: { userId: string }) {
    const { t, i18n } = useTranslation();
    const auth = useAuth();
    const client = useQueryClient();
    const profile = useQuery(profileQueries.detail(userId));
    const [name, setName] = useState('');
    const [culture, setCulture] = useState<UserProfile['preferredCulture']>('en-US');
    const [password, setPassword] = useState('');
    const [currentPassword, setCurrentPassword] = useState('');
    const [newPassword, setNewPassword] = useState('');
    const [confirmPassword, setConfirmPassword] = useState('');
    const [code, setCode] = useState('');
    const [setup, setSetup] = useState<{ sharedKey: string; authenticatorUri: string }>();
    const [setupRequested, setSetupRequested] = useState(false);
    const [recoveryCodes, setRecoveryCodes] = useState<string[]>();
    const [securityAction, setSecurityAction] = useState<'disable' | 'regenerate'>();
    const [revokeSessionsRequested, setRevokeSessionsRequested] = useState(false);
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');
    const fileRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        if (!profile.data) return;
        setName(profile.data.displayName);
        setCulture(profile.data.preferredCulture);
    }, [profile.data]);

    const save = useMutation({
        mutationFn: () => profileApi.update(name, culture),
        onSuccess: async () => {
            await client.invalidateQueries({ queryKey: profileKeys.detail(userId) });
            await auth.refresh();
            const language = culture.split('-')[0];
            await i18n.changeLanguage(language);
            setMessage(t('profile.saved')); setError('');
        },
        onError: () => setError(t('profile.saveError')),
    });
    const pictureMutation = useMutation({
        mutationFn: profileApi.setPicture,
        onSuccess: async () => { await client.invalidateQueries({ queryKey: profileKeys.detail(userId) }); await client.invalidateQueries({ queryKey: profileKeys.picture(userId) }); setMessage(t('profile.saved')); setError(''); },
        onError: () => setError(t('profile.pictureInvalid')),
    });
    const removePicture = useMutation({
        mutationFn: profileApi.deletePicture,
        onSuccess: async () => { await client.invalidateQueries({ queryKey: profileKeys.detail(userId) }); await client.removeQueries({ queryKey: profileKeys.picture(userId) }); setMessage(t('profile.saved')); },
        onError: () => setError(t('profile.saveError')),
    });
    const setupMutation = useMutation({ mutationFn: profileApi.setupAuthenticator, onSuccess: result => { setSetup(result); setPassword(''); setError(''); }, onError: () => setError(t('profile.securityFailed')) });
    const enableMutation = useMutation({ mutationFn: profileApi.enableAuthenticator, onSuccess: async result => { setRecoveryCodes(result.recoveryCodes); setSetup(undefined); setCode(''); await client.invalidateQueries({ queryKey: profileKeys.detail(userId) }); setError(''); }, onError: () => setError(t('profile.codeInvalid')) });
    const securityMutation = useMutation<{ recoveryCodes: string[] } | void>({
        mutationFn: async () => {
            if (securityAction === 'disable') { await profileApi.disableAuthenticator(password, code); return undefined; }
            return await profileApi.regenerateRecoveryCodes(password, code);
        },
        onSuccess: async result => {
            if (securityAction === 'regenerate' && result) setRecoveryCodes(result.recoveryCodes);
            else await client.invalidateQueries({ queryKey: profileKeys.detail(userId) });
            setPassword(''); setCode(''); setSecurityAction(undefined); setError('');
        },
        onError: () => setError(t('profile.securityFailed')),
    });
    const passwordMutation = useMutation({
        mutationFn: () => profileApi.changePassword(currentPassword, newPassword),
        onSuccess: () => { setCurrentPassword(''); setNewPassword(''); setConfirmPassword(''); setMessage(t('profile.passwordChanged')); setError(''); },
        onError: () => setError(t('profile.saveError')),
    });
    const sessionsMutation = useMutation({
        mutationFn: () => profileApi.revokeOtherDevices(currentPassword),
        onSuccess: () => { setCurrentPassword(''); setRevokeSessionsRequested(false); setMessage(t('profile.sessionsRevoked')); setError(''); },
        onError: () => setError(t('profile.securityFailed')),
    });

    function submit(event: FormEvent<HTMLFormElement>) { event.preventDefault(); setMessage(''); setError(''); save.mutate(); }
    const isBusy = save.isPending || pictureMutation.isPending || removePicture.isPending;
    const displayName = profile.data?.displayName || profile.data?.email || '';
    const initials = displayName.trim().split(/\s+/).slice(0, 2).map(part => part[0]).join('').toUpperCase();

    if (profile.isPending) return <p role="status" className="text-sm text-slate-600">{t('common.status.loading')}</p>;
    if (profile.isError || !profile.data) return <p role="alert" className="text-sm text-red-700">{t('profile.loadError')}</p>;

    return <div className="mx-auto grid max-w-6xl gap-4 lg:grid-cols-[260px_minmax(0,1fr)]">
        <AverioCard className="h-fit text-center lg:sticky lg:top-24">
            <ProfileAvatar userId={userId} initials={initials} className="mx-auto mb-3 grid size-20 place-items-center rounded-[24px] bg-slate-800 text-2xl font-black text-white" />
            <h2 className="text-base font-bold">{displayName}</h2>
            <p className="mt-1 break-all text-xs text-slate-500">{profile.data.email}</p>
            <span className={`mt-3 inline-flex rounded-full px-3 py-1 text-xs font-semibold ${profile.data.emailVerified ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/40 dark:text-emerald-200' : 'bg-amber-100 text-amber-800'}`}>{t(profile.data.emailVerified ? 'profile.verified' : 'profile.notVerified')}</span>
        </AverioCard>

        <div className="space-y-4">
            <AverioCard>
                <div className="mb-5"><h3 className="text-base font-bold">{t('profile.personal')}</h3><p className="mt-1 text-sm text-slate-500">{t('profile.personalHelp')}</p></div>
                <form onSubmit={submit} className="grid gap-4 sm:grid-cols-2">
                    <AverioField>{t('profile.name')}<AverioInput autoComplete="name" maxLength={80} required value={name} onChange={event => setName(event.target.value)} /></AverioField>
                    <AverioField>{t('profile.email')}<AverioInput type="email" value={profile.data.email} disabled readOnly /></AverioField>
                    <AverioField>{t('profile.language')}<AverioSelect value={culture} onChange={event => setCulture(event.target.value as UserProfile['preferredCulture'])}>
                        <option value="en-US">{t('profile.languageEnglish')}</option><option value="de-DE">{t('profile.languageGerman')}</option><option value="hu-HU">{t('profile.languageHungarian')}</option>
                    </AverioSelect></AverioField>
                    <div className="flex items-end"><AverioButton type="submit" variant="primary" disabled={isBusy}>{t('profile.save')}</AverioButton></div>
                </form>
                <div className="mt-6 border-t border-slate-200 pt-5 dark:border-slate-700">
                    <h4 className="text-sm font-semibold">{t('profile.picture')}</h4><p className="mt-1 text-sm text-slate-500">{t('profile.pictureHelp')}</p>
                    <div className="mt-3 flex flex-wrap gap-2">
                        <input ref={fileRef} type="file" accept="image/jpeg,image/png,image/webp" className="sr-only" aria-label={t('profile.uploadPicture')}
                            onChange={event => { const file = event.target.files?.[0]; if (file) { if (file.size > 5 * 1024 * 1024) setError(t('profile.pictureInvalid')); else pictureMutation.mutate(file); } event.target.value = ''; }} />
                        <AverioButton onClick={() => fileRef.current?.click()} disabled={isBusy}>{t('profile.uploadPicture')}</AverioButton>
                        {profile.data.hasProfilePicture && <AverioButton onClick={() => removePicture.mutate()} disabled={isBusy}>{t('profile.removePicture')}</AverioButton>}
                    </div>
                </div>
            </AverioCard>

            <AverioCard>
                <div className="mb-3"><h3 className="text-base font-bold">{t('profile.security')}</h3><p className="mt-1 text-sm text-slate-500">{t('profile.securityHelp')}</p></div>
                <form onSubmit={event => { event.preventDefault(); if (newPassword !== confirmPassword) { setError(t('profile.passwordsDoNotMatch')); return; } setError(''); passwordMutation.mutate(); }} className="grid gap-3 border-t border-slate-200 py-4 dark:border-slate-700 sm:grid-cols-2">
                    <AverioField>{t('profile.currentPassword')}<AverioInput type="password" autoComplete="current-password" required value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></AverioField>
                    <AverioField>{t('profile.newPassword')}<AverioInput type="password" autoComplete="new-password" required minLength={12} value={newPassword} onChange={event => setNewPassword(event.target.value)} /></AverioField>
                    <AverioField>{t('profile.confirmNewPassword')}<AverioInput type="password" autoComplete="new-password" required minLength={12} value={confirmPassword} onChange={event => setConfirmPassword(event.target.value)} /></AverioField>
                    <div className="flex items-end"><AverioButton type="submit" disabled={passwordMutation.isPending}>{t('profile.changePassword')}</AverioButton></div>
                </form>
                <div className="flex flex-wrap items-center gap-3 border-t border-slate-200 py-4 dark:border-slate-700">
                    <div className="min-w-0 flex-1"><h4 className="text-sm font-semibold">{t('profile.mfa')}</h4><p className="mt-1 text-sm text-slate-500">{t(profile.data.twoFactorEnabled ? 'profile.mfaOn' : 'profile.mfaOff')}</p></div>
                    <span className="rounded-full bg-slate-100 px-3 py-1 text-xs font-semibold dark:bg-slate-800">{t(profile.data.twoFactorEnabled ? 'profile.enabled' : 'profile.disabled')}</span>
                    <AverioButton onClick={() => { if (profile.data.twoFactorEnabled) setSecurityAction('disable'); else setSetupRequested(true); setError(''); setPassword(''); setCode(''); }}>{t(profile.data.twoFactorEnabled ? 'profile.disable' : 'profile.setup')}</AverioButton>
                </div>

                {!profile.data.twoFactorEnabled && !setup && setupRequested && <form onSubmit={event => { event.preventDefault(); setupMutation.mutate(password); }} className="grid gap-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-900/60 sm:grid-cols-[1fr_auto]">
                    <AverioField>{t('profile.passwordConfirm')}<AverioInput type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /></AverioField>
                    <div className="flex gap-2 self-end"><AverioButton type="submit" disabled={setupMutation.isPending}>{t('profile.setup')}</AverioButton><AverioButton onClick={() => setSetupRequested(false)}>{t('profile.cancel')}</AverioButton></div>
                </form>}

                {setup && <form onSubmit={event => { event.preventDefault(); enableMutation.mutate(code); }} className="space-y-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-900/60">
                    <h4 className="text-sm font-semibold">{t('profile.setupTitle')}</h4><p className="text-sm text-slate-500">{t('profile.setupHelp')}</p>
                    <a href={setup.authenticatorUri} className="inline-flex min-h-11 items-center rounded-[11px] border border-[#E4E7EC] px-3 text-sm font-semibold text-primary-700 underline dark:border-[#233044] dark:text-primary-300">{t('profile.openAuthenticator')}</a>
                    <AverioField>{t('profile.sharedKey')}<AverioInput readOnly value={setup.sharedKey} onFocus={event => event.currentTarget.select()} /></AverioField>
                    <AverioField>{t('profile.code')}<AverioInput inputMode="numeric" autoComplete="one-time-code" maxLength={9} required value={code} onChange={event => setCode(event.target.value)} /></AverioField>
                    <div className="flex flex-wrap gap-2"><AverioButton type="submit" variant="primary" disabled={enableMutation.isPending}>{t('profile.verifyEnable')}</AverioButton><AverioButton onClick={() => { setSetup(undefined); setSetupRequested(false); setPassword(''); setCode(''); }}>{t('profile.cancel')}</AverioButton></div>
                </form>}

                {securityAction && <form onSubmit={event => { event.preventDefault(); securityMutation.mutate(); }} className="space-y-3 rounded-xl bg-slate-50 p-4 dark:bg-slate-900/60">
                    <p className="text-sm text-slate-500">{t(securityAction === 'disable' ? 'profile.disableHelp' : 'profile.regenerateHelp')}</p>
                    <AverioField>{t('profile.passwordConfirm')}<AverioInput type="password" autoComplete="current-password" required value={password} onChange={event => setPassword(event.target.value)} /></AverioField>
                    <AverioField>{t('profile.code')}<AverioInput inputMode="numeric" autoComplete="one-time-code" required value={code} onChange={event => setCode(event.target.value)} /></AverioField>
                    <div className="flex gap-2"><AverioButton type="submit" variant={securityAction === 'disable' ? 'danger' : 'primary'} disabled={securityMutation.isPending}>{t(securityAction === 'disable' ? 'profile.disable' : 'profile.regenerate')}</AverioButton><AverioButton onClick={() => setSecurityAction(undefined)}>{t('profile.cancel')}</AverioButton></div>
                </form>}

                {profile.data.twoFactorEnabled && !securityAction && <AverioButton className="mt-2" onClick={() => { setSecurityAction('regenerate'); setPassword(''); setCode(''); }}>{t('profile.regenerate')}</AverioButton>}
                <div className="mt-5 border-t border-slate-200 pt-4 dark:border-slate-700">
                    <h4 className="text-sm font-semibold">{t('profile.sessions')}</h4><p className="mt-1 text-sm text-slate-500">{t('profile.sessionsHelp')}</p>
                    {!revokeSessionsRequested ? <AverioButton className="mt-3" onClick={() => { setRevokeSessionsRequested(true); setCurrentPassword(''); setError(''); }}>{t('profile.revokeSessions')}</AverioButton> :
                        <form onSubmit={event => { event.preventDefault(); sessionsMutation.mutate(); }} className="mt-3 grid gap-3 sm:grid-cols-[1fr_auto]">
                            <AverioField>{t('profile.passwordConfirm')}<AverioInput type="password" autoComplete="current-password" required value={currentPassword} onChange={event => setCurrentPassword(event.target.value)} /></AverioField>
                            <div className="flex gap-2 self-end"><AverioButton type="submit" variant="danger" disabled={sessionsMutation.isPending}>{t('profile.revokeSessions')}</AverioButton><AverioButton onClick={() => setRevokeSessionsRequested(false)}>{t('profile.cancel')}</AverioButton></div>
                        </form>}
                </div>
                {recoveryCodes && <div className="mt-4 space-y-3 rounded-xl border border-amber-300 bg-amber-50 p-4 text-amber-950 dark:bg-amber-950/30 dark:text-amber-100">
                    <h4 className="font-semibold">{t('profile.recoveryTitle')}</h4><p className="text-sm">{t('profile.recoveryHelp')}</p>
                    <code className="grid grid-cols-2 gap-x-4 gap-y-1 rounded-lg bg-white p-3 font-mono text-sm dark:bg-slate-900">{recoveryCodes.map(item => <span key={item}>{item}</span>)}</code>
                    <div className="flex flex-wrap gap-2"><AverioButton onClick={() => void navigator.clipboard.writeText(recoveryCodes.join('\n'))}>{t('profile.copyCodes')}</AverioButton><AverioButton onClick={() => setRecoveryCodes(undefined)}>{t('profile.done')}</AverioButton></div>
                </div>}
            </AverioCard>
            {(message || error) && <p role={error ? 'alert' : 'status'} className={`text-sm ${error ? 'text-red-700 dark:text-red-300' : 'text-emerald-700 dark:text-emerald-300'}`}>{error || message}</p>}
        </div>
    </div>;
}

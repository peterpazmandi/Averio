import { apiFetch, apiFetchResponse } from '../../shared/api/apiFetch';

export interface UserProfile {
    userId: string;
    displayName: string;
    email: string;
    emailVerified: boolean;
    preferredCulture: 'en-US' | 'de-DE' | 'hu-HU';
    twoFactorEnabled: boolean;
    hasProfilePicture: boolean;
}

export interface AuthenticatorSetup {
    sharedKey: string;
    authenticatorUri: string;
}

export interface RecoveryCodes {
    recoveryCodes: string[];
}

export const profileApi = {
    get: (signal?: AbortSignal) => apiFetch<UserProfile>('/api/v1/profile', { signal }),
    update: (displayName: string, preferredCulture: UserProfile['preferredCulture']) => apiFetch<void>('/api/v1/profile', {
        method: 'PUT', body: JSON.stringify({ displayName, preferredCulture }),
    }),
    changePassword: (currentPassword: string, newPassword: string) => apiFetch<void>('/api/v1/profile/password', {
        method: 'POST', body: JSON.stringify({ currentPassword, newPassword }),
    }),
    revokeOtherDevices: (password: string) => apiFetch<void>('/api/v1/profile/sessions/revoke-other-devices', {
        method: 'POST', body: JSON.stringify({ password }),
    }),
    getPicture: async (signal?: AbortSignal) => (await apiFetchResponse('/api/v1/profile/picture', { signal })).blob(),
    setPicture: async (file: File) => {
        const body = new FormData();
        body.append('file', file);
        await apiFetchResponse('/api/v1/profile/picture', { method: 'PUT', body });
    },
    deletePicture: () => apiFetch<void>('/api/v1/profile/picture', { method: 'DELETE' }),
    setupAuthenticator: (password: string) => apiFetch<AuthenticatorSetup>('/api/v1/profile/authenticator/setup', {
        method: 'POST', body: JSON.stringify({ password }),
    }),
    enableAuthenticator: (code: string) => apiFetch<RecoveryCodes>('/api/v1/profile/authenticator/enable', {
        method: 'POST', body: JSON.stringify({ code }),
    }),
    disableAuthenticator: (password: string, code: string) => apiFetch<void>('/api/v1/profile/authenticator/disable', {
        method: 'POST', body: JSON.stringify({ password, code }),
    }),
    regenerateRecoveryCodes: (password: string, code: string) => apiFetch<RecoveryCodes>('/api/v1/profile/authenticator/recovery-codes', {
        method: 'POST', body: JSON.stringify({ password, code }),
    }),
};

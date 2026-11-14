import { apiFetch, apiFetchResponse } from '../../../shared/api/apiFetch';
import { clearCsrfToken } from '../../../shared/api/csrf';

export interface PasskeyInfo {
    credentialId: string;
    name: string;
    createdAt: string;
}

export function passkeysSupported(): boolean {
    return window.isSecureContext && typeof window.PublicKeyCredential !== 'undefined'
        && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function'
        && typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function';
}

export async function createPasskey(password: string, name: string): Promise<void> {
    const options = await apiFetch<Parameters<typeof PublicKeyCredential.parseCreationOptionsFromJSON>[0]>(
        '/api/v1/profile/passkeys/options', { method: 'POST', body: JSON.stringify({ password }) },
    );
    const credential = await navigator.credentials.create({
        publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options),
    });
    if (!credential) throw new Error('Passkey creation was cancelled.');
    await apiFetch<PasskeyInfo>('/api/v1/profile/passkeys', {
        method: 'POST', body: JSON.stringify({ credentialJson: JSON.stringify(credential), name }),
    });
}

export async function signInWithPasskey(rememberMe: boolean): Promise<void> {
    const options = await apiFetch<Parameters<typeof PublicKeyCredential.parseRequestOptionsFromJSON>[0]>(
        '/api/v1/auth/passkey/options', { method: 'POST' },
    );
    const credential = await navigator.credentials.get({
        publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options),
    });
    if (!credential) throw new Error('Passkey sign-in was cancelled.');
    await apiFetch<void>('/api/v1/auth/passkey', {
        method: 'POST', body: JSON.stringify({ credentialJson: JSON.stringify(credential), rememberMe }),
    });
    clearCsrfToken();
}

export const passkeyApi = {
    list: (signal?: AbortSignal) => apiFetch<PasskeyInfo[]>('/api/v1/profile/passkeys', { signal }),
    remove: async (credentialId: string, password: string) => {
        await apiFetchResponse(`/api/v1/profile/passkeys/${encodeURIComponent(credentialId)}`, {
            method: 'DELETE', body: JSON.stringify({ password }),
        });
    },
};

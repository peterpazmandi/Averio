import { apiFetch } from '../../../shared/api/apiFetch';
import { clearCsrfToken } from '../../../shared/api/csrf';
import { PasskeysApi, type PasskeyResponse } from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';

const api = new PasskeysApi(generatedApiConfiguration);
export type PasskeyInfo = PasskeyResponse;

export function passkeysSupported(): boolean {
    return window.isSecureContext && typeof window.PublicKeyCredential !== 'undefined'
        && typeof PublicKeyCredential.parseCreationOptionsFromJSON === 'function'
        && typeof PublicKeyCredential.parseRequestOptionsFromJSON === 'function';
}

export async function createPasskey(password: string, name: string): Promise<void> {
    // These two endpoints return browser-defined WebAuthn JSON rather than a
    // fixed application DTO, so the generated client exposes them as void.
    const options = await apiFetch<Parameters<typeof PublicKeyCredential.parseCreationOptionsFromJSON>[0]>(
        '/api/v1/profile/passkeys/options', { method: 'POST', body: JSON.stringify({ password }) },
    );
    const credential = await navigator.credentials.create({
        publicKey: PublicKeyCredential.parseCreationOptionsFromJSON(options),
    });
    if (!credential) throw new Error('Passkey creation was cancelled.');
    await api.apiV1ProfilePasskeysPost({ registerPasskeyRequest: { credentialJson: JSON.stringify(credential), name } });
}

export async function signInWithPasskey(rememberMe: boolean): Promise<void> {
    const options = await apiFetch<Parameters<typeof PublicKeyCredential.parseRequestOptionsFromJSON>[0]>(
        '/api/v1/auth/passkey/options', { method: 'POST' },
    );
    const credential = await navigator.credentials.get({
        publicKey: PublicKeyCredential.parseRequestOptionsFromJSON(options),
    });
    if (!credential) throw new Error('Passkey sign-in was cancelled.');
    await api.apiV1AuthPasskeyPost({ passkeySignInRequest: { credentialJson: JSON.stringify(credential), rememberMe } });
    clearCsrfToken();
}

export const passkeyApi = {
    list: (signal?: AbortSignal) => api.apiV1ProfilePasskeysGet({ signal }),
    remove: (credentialId: string, password: string) => api.apiV1ProfilePasskeysCredentialIdDelete({
        credentialId, removePasskeyRequest: { password },
    }),
};

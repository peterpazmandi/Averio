import { API_BASE_URL } from './apiConfiguration';
import type { CsrfTokenResponse } from './generated';

let cachedToken: string | null = null;
let tokenPromise: Promise<string> | null = null;
let identityGeneration = 0;

export async function getCsrfToken(): Promise<string> {
    for (;;) {
        if (cachedToken) return cachedToken;
        const generation = identityGeneration;
        const request = tokenPromise ??= loadCsrfToken();
        try {
            const token = await request;
            // A session change can finish while the previous token is loading.
            // All waiters must obtain a token for the new identity instead.
            if (generation !== identityGeneration) continue;
            cachedToken = token;
            return token;
        }
        catch (error) {
            if (generation === identityGeneration) throw error;
        }
        finally {
            if (tokenPromise === request) tokenPromise = null;
        }
    }
}

export function clearCsrfToken(): void {
    identityGeneration += 1;
    cachedToken = null;
    tokenPromise = null;
}

async function loadCsrfToken(): Promise<string> {
    const response = await fetch(`${API_BASE_URL}/api/v1/security/csrf`, {
        method: 'GET', credentials: 'include', headers: { Accept: 'application/json' },
    });
    if (!response.ok) throw new Error('Could not retrieve CSRF token.');
    const data: CsrfTokenResponse = await response.json();
    return data.token;
}

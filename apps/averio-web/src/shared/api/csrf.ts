import { API_BASE_URL } from './apiConfiguration';
import type { CsrfTokenResponse } from './generated';

let cachedToken: string | null = null;
let tokenPromise: Promise<string> | null = null;
let identityGeneration = 0;

export async function getCsrfToken(): Promise<string> {
    if (cachedToken) return cachedToken;
    const generation = identityGeneration;
    const request = tokenPromise ??= loadCsrfToken();
    try {
        const token = await request;
        // Never let an action started by one identity use another user's token.
        if (generation !== identityGeneration) throw sessionChanged();
        cachedToken = token;
        return token;
    }
    catch (error) {
        if (generation !== identityGeneration) throw sessionChanged();
        throw error;
    }
    finally {
        if (tokenPromise === request) tokenPromise = null;
    }
}

function sessionChanged(): DOMException {
    return new DOMException('The session changed while retrieving the CSRF token.', 'AbortError');
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

import {
    API_BASE_URL,
} from './apiConfiguration';

interface CsrfTokenResponse {
    token: string;
}

let cachedToken: string | null = null;

let tokenPromise: Promise<string> | null = null;

export async function getCsrfToken(): Promise<string> {
    if (cachedToken) {
        return cachedToken;
    }

    if (tokenPromise) {
        return tokenPromise;
    }

    tokenPromise = loadCsrfToken();

    try {
        cachedToken = await tokenPromise;

        return cachedToken;
    }
    finally {
        tokenPromise = null;
    }
}

export function clearCsrfToken(): void {
    cachedToken = null;
    tokenPromise = null;
}

async function loadCsrfToken(): Promise<string> {
    const response =
        await fetch(
            `${API_BASE_URL}/api/v1/security/csrf`,
            {
                method: 'GET',
                credentials: 'include',
                headers: {
                    Accept: 'application/json',
                },
            },
        );

    if (!response.ok) {
        throw new Error('Could not retrieve CSRF token.');
    }

    const data: CsrfTokenResponse = await response.json();

    return data.token;
}
import {
    API_BASE_URL,
    getApiCulture,
} from './apiConfiguration';

import {
    ApiError,
    type ProblemDetails,
} from './ApiError';

export async function apiFetch<T>(
    path: string,
    init?: RequestInit,
): Promise<T> {
    const response = await fetch(
        `${API_BASE_URL}${path}`,
        {
            ...init,

            credentials: 'include',

            headers: {
                Accept: 'application/json',
                'Accept-Language': getApiCulture(),
                ...init?.headers,
            },
        },
    );

    if (!response.ok) {
        let problem: ProblemDetails | undefined;

        try {
            problem =
                await response.json() as ProblemDetails;
        }
        catch {
            // Response did not contain ProblemDetails.
        }

        throw new ApiError(
            problem?.detail
                ?? problem?.title
                ?? `Request failed with status ${response.status}.`,
            response.status,
            problem?.code,
            problem?.traceId,
        );
    }

    if (response.status === 204) {
        return undefined as T;
    }

    return await response.json() as T;
}
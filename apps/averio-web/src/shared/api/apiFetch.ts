import { API_BASE_URL, getApiCulture, } from './apiConfiguration';
import { ApiError, type ProblemDetails, } from './ApiError';
import { getCsrfToken, } from './csrf';

const unsafeMethods =
    new Set([
        'POST',
        'PUT',
        'PATCH',
        'DELETE',
    ]);

export async function apiFetchResponse(
    path: string,
    init?: RequestInit,
): Promise<Response> {
    const method = (init?.method ?? 'GET').toUpperCase();

    const headers = new Headers(init?.headers);
    headers.set('Accept', 'application/json');
    headers.set('Accept-Language', getApiCulture());

    if (
        init?.body
        && !headers.has('Content-Type')
        && !(init.body instanceof FormData)
    ) {
        headers.set(
            'Content-Type',
            'application/json',
        );
    }

    if (unsafeMethods.has(method)) {
        const csrfToken = await getCsrfToken();

        headers.set('X-CSRF-TOKEN', csrfToken);
    }

    const response =
        await fetch(
            `${API_BASE_URL}${path}`,
            {
                ...init,
                method,
                headers,
                credentials: 'include',
            },
        );

    if (!response.ok) {
        let problem: ProblemDetails | undefined;

        try {
            problem = await response.json() as ProblemDetails;
        }
        catch {
            // No ProblemDetails payload.
        }

        const firstValidationMessage =
            problem?.errors
                ? Object.values(problem.errors)
                    .flat()
                    .at(0)
                : undefined;

        throw new ApiError(
            problem?.detail
            ?? firstValidationMessage
            ?? problem?.title
            ?? `Request failed with status ${response.status}.`,
            response.status,
            problem?.code,
            problem?.traceId,
            problem?.errors,
        );
    }

    return response;
}

export async function apiFetch<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await apiFetchResponse(path, init);
    if (response.status === 204) {
        return undefined as T;
    }

    return await response.json() as T;
}
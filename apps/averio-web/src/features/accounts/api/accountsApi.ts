import { apiFetch } from '../../../shared/api/apiFetch';

import type {
    AccountResponse,
    CreateAccountRequest,
    CreateAccountResponse,
} from '../../../shared/api/generated';

export function getAccounts(
    householdId: string,
    includeArchived = false,
    signal?: AbortSignal,
): Promise<AccountResponse[]> {
    const id = encodeURIComponent(householdId);

    return apiFetch<AccountResponse[]>(
        `/api/v1/households/${id}/accounts`
            + `?includeArchived=${includeArchived}`,
        { signal },
    );
}

export function createAccount(
    householdId: string,
    request: CreateAccountRequest,
): Promise<CreateAccountResponse> {
    const id = encodeURIComponent(householdId);

    return apiFetch<CreateAccountResponse>(
        `/api/v1/households/${id}/accounts`,
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}
import { AccountsApi, AccountOwnerOptionsApi, Configuration } from '../../../shared/api/generated';
import type { CreateAccountRequest, UpdateAccountRequest } from '../../../shared/api/generated';
import { API_BASE_URL } from '../../../shared/api/apiConfiguration';
import { apiFetchResponse } from '../../../shared/api/apiFetch';

// Keep the generated transport's serialization and the shared cookie/CSRF/error policy.
const configuration = new Configuration({
    basePath: API_BASE_URL,
    fetchApi: (url, init) => apiFetchResponse(String(url).slice(API_BASE_URL.length), init),
    // Preserve localized Problem Details instead of the generated runtime's FetchError wrapper.
    middleware: [{ onError: async ({ error }) => { throw error; } }],
});
const accounts = new AccountsApi(configuration);
const owners = new AccountOwnerOptionsApi(configuration);

export const getAccounts = (householdId: string, includeArchived = false, signal?: AbortSignal) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsGet({ householdId, includeArchived }, { signal });

export const getAccount = (householdId: string, accountId: string, signal?: AbortSignal) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdGet({ householdId, accountId }, { signal });

export const createAccount = (householdId: string, request: CreateAccountRequest) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsPost({ householdId, createAccountRequest: request });

export const updateAccount = (householdId: string, accountId: string, request: UpdateAccountRequest) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdPut({ householdId, accountId, updateAccountRequest: request });

export const archiveAccount = (householdId: string, accountId: string) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdArchivePut({ householdId, accountId });

export const restoreAccount = (householdId: string, accountId: string) =>
    accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdArchiveDelete({ householdId, accountId });

export const getAccountOwnerOptions = (householdId: string, signal?: AbortSignal) =>
    owners.apiV1HouseholdsHouseholdIdAccountOwnerOptionsGet({ householdId }, { signal });

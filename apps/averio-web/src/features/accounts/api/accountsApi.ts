import {
    AccountsApi,
    AccountOwnerOptionsApi,
} from '../../../shared/api/generated';
import type {
    AccountResponse,
    CreateAccountRequest,
    UpdateAccountRequest,
} from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';

export type AccountData = AccountResponse;

const accounts = new AccountsApi(generatedApiConfiguration);
const owners = new AccountOwnerOptionsApi(generatedApiConfiguration);

export const getAccounts = (
    householdId: string,
    includeArchived = false,
    signal?: AbortSignal,
) => {
    return accounts.apiV1HouseholdsHouseholdIdAccountsGet(
        { householdId, includeArchived },
        { signal },
    );
};

export const getAccount = (
    householdId: string,
    accountId: string,
    signal?: AbortSignal,
) => accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdGet(
    { householdId, accountId },
    { signal },
);

export const createAccount = (
    householdId: string,
    request: CreateAccountRequest,
) => accounts.apiV1HouseholdsHouseholdIdAccountsPost({
    householdId,
    createAccountRequest: request,
});

export const updateAccount = (
    householdId: string,
    accountId: string,
    request: UpdateAccountRequest,
) => accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdPut({
    householdId,
    accountId,
    updateAccountRequest: request,
});

export const archiveAccount = (
    householdId: string,
    accountId: string,
) => accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdArchivePut({
    householdId,
    accountId,
});

export const restoreAccount = (
    householdId: string,
    accountId: string,
) => accounts.apiV1HouseholdsHouseholdIdAccountsAccountIdArchiveDelete({
    householdId,
    accountId,
});

export const getAccountOwnerOptions = (
    householdId: string,
    signal?: AbortSignal,
) => owners.apiV1HouseholdsHouseholdIdAccountOwnerOptionsGet(
    { householdId },
    { signal },
);

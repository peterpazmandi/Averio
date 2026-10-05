import { queryOptions } from '@tanstack/react-query';
import { HouseholdsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';

export const householdApi = new HouseholdsApi(generatedApiConfiguration);
export const householdKeys = {
    all: ['household-settings'] as const,
    detail: (userId: string, householdId: string) => ['household-settings', userId, householdId] as const,
};
export const householdQueries = {
    detail: (userId: string, householdId: string) => queryOptions({
        queryKey: householdKeys.detail(userId, householdId),
        queryFn: ({ signal }) => householdApi.apiV1HouseholdsHouseholdIdGet({ householdId }, { signal }),
    }),
};

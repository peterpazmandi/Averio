import { queryOptions } from '@tanstack/react-query';
import { SavedTransactionViewsApi, LabelsApi } from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';
export const savedViewsApi = new SavedTransactionViewsApi(generatedApiConfiguration);
const labels = new LabelsApi(generatedApiConfiguration);
export const savedViewQueries = {
    list: (userId: string, householdId: string) => queryOptions({
        queryKey: ['saved-transaction-views', userId, householdId],
        queryFn: ({ signal }) => savedViewsApi.apiV1HouseholdsHouseholdIdSavedTransactionViewsGet({ householdId }, { signal }),
    }),
    labels: (userId: string, householdId: string) => queryOptions({
        queryKey: ['labels', userId, householdId, 'filter-options'],
        queryFn: ({ signal }) => labels.apiV1HouseholdsHouseholdIdLabelsFilterOptionsGet({ householdId }, { signal }),
    }),
};

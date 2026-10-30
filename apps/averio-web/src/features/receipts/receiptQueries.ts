import { ReceiptsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { queryOptions } from '@tanstack/react-query';

export const receiptsApi = new ReceiptsApi(generatedApiConfiguration);
export const receiptKeys = {
    all: (userId: string, householdId: string) => ['receipts', userId, householdId] as const,
};
export const receiptQueries = {
    capabilities: (userId: string, householdId: string) => queryOptions({
        queryKey: [...receiptKeys.all(userId, householdId), 'capabilities'],
        queryFn: ({ signal }) => receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansCapabilitiesGet({ householdId }, { signal }),
    }),
};

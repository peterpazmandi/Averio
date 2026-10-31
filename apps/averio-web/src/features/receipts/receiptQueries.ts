import { ReceiptsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { receiptLog, receiptRequest } from './receiptDiagnostics';
import { queryOptions } from '@tanstack/react-query';

export const receiptsApi = new ReceiptsApi(generatedApiConfiguration);
export const receiptKeys = {
    all: (userId: string, householdId: string) => ['receipts', userId, householdId] as const,
};
export const receiptQueries = {
    capabilities: (userId: string, householdId: string) => queryOptions({
        queryKey: [...receiptKeys.all(userId, householdId), 'capabilities'],
        refetchInterval: 60_000, // Refresh shared usage and unlock at the next UTC month.
        queryFn: async ({ signal }) => {
            const result = await receiptRequest('capabilities', {}, () => receiptsApi.apiV1HouseholdsHouseholdIdReceiptScansCapabilitiesGet({ householdId }, { signal }), true);
            receiptLog('capabilities.state', { enabled: result.enabled }, 'debug');
            return result;
        },
    }),
};

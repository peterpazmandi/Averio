import { queryOptions } from '@tanstack/react-query';
import { ItemRefundsApi } from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';
export const itemRefundsApi = new ItemRefundsApi(generatedApiConfiguration);
export const itemRefundKeys = {
    all: (user: string, household: string) => ['item-refunds', user, household] as const,
    receipt: (user: string, household: string, transaction: string) => [...itemRefundKeys.all(user, household), transaction] as const,
    credits: (user: string, household: string, account: string) => [...itemRefundKeys.all(user, household), 'credits', account] as const,
};
export const itemRefundQueries = {
    receipt: (user: string, householdId: string, transactionId: string) => queryOptions({
        queryKey: itemRefundKeys.receipt(user, householdId, transactionId),
        queryFn: ({ signal }) => itemRefundsApi.apiV1HouseholdsHouseholdIdTransactionsTransactionIdRefundsGet({ householdId, transactionId }, { signal }),
    }),
};

import { queryOptions } from '@tanstack/react-query';
import { ReimbursementsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
export const reimbursementsApi = new ReimbursementsApi(generatedApiConfiguration);
export const reimbursementKeys = { household: (userId: string, householdId: string) => ['reimbursements', userId, householdId] as const };
export const reimbursementQueries = {
    list: (userId: string, householdId: string, transactionId?: string) => queryOptions({
        queryKey: [...reimbursementKeys.household(userId, householdId), transactionId ?? 'all'],
        queryFn: ({ signal }) => reimbursementsApi.apiV1HouseholdsHouseholdIdReimbursementsGet({ householdId, transactionId }, { signal }),
        refetchInterval: 30_000,
    }),
};

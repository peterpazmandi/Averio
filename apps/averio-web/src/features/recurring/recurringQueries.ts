import { queryOptions } from '@tanstack/react-query';
import { RecurringApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
export const recurringApi = new RecurringApi(generatedApiConfiguration);
export const recurringQueries = {
    list: (userId: string, householdId: string) => queryOptions({
        queryKey: ['recurring', userId, householdId],
        queryFn: ({ signal }) => recurringApi.apiV1HouseholdsHouseholdIdRecurringGet({ householdId }, { signal }),
        refetchInterval: 30_000,
    }),
    history: (userId: string, householdId: string, ruleId: string) => queryOptions({
        queryKey: ['recurring', userId, householdId, 'history', ruleId],
        queryFn: ({ signal }) => recurringApi.apiV1HouseholdsHouseholdIdRecurringRuleIdOccurrencesGet({ householdId, ruleId }, { signal }),
    }),
};

import { queryOptions } from '@tanstack/react-query';
import { PersonalMoneyApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';

export const personalMoneyApi = new PersonalMoneyApi(generatedApiConfiguration);

export const personalMoneyKeys = {
    mine: (userId: string, householdId: string) => ['personal-money', userId, householdId, 'mine'] as const,
    boundary: (userId: string, householdId: string) => ['statistics', userId, householdId, 'boundary-flow'] as const,
};

export const personalMoneyQueries = {
    summary: (userId: string, householdId: string) => queryOptions({
        // A descendant of mine so existing wallet mutation invalidations refresh both views.
        queryKey: [...personalMoneyKeys.mine(userId, householdId), 'summary'],
        queryFn: async ({ signal }) => {
            const result = await personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeSummaryGetRaw(
                { householdId }, { signal });
            return result.raw.status === 204 ? null : result.value();
        },
    }),
    mine: (userId: string, householdId: string) => queryOptions({
        queryKey: personalMoneyKeys.mine(userId, householdId),
        queryFn: async ({ signal }) => {
            // The API deliberately returns 204 until the first grant exists.
            const result = await personalMoneyApi.apiV1HouseholdsHouseholdIdPersonalMoneyMeGetRaw(
                { householdId }, { signal });
            return result.raw.status === 204 ? null : result.value();
        },
    }),
};

export const recurringAllowanceKeys = {
    list: (userId: string, householdId: string) =>
        ['personal-money', userId, householdId, 'schedules'] as const,
    history: (userId: string, householdId: string, id: string) =>
        ['personal-money', userId, householdId, 'schedules', id, 'history'] as const,
};

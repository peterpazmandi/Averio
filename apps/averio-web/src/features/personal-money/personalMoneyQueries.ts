import { queryOptions } from '@tanstack/react-query';
import { PersonalMoneyApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';

export const personalMoneyApi = new PersonalMoneyApi(generatedApiConfiguration);

export const personalMoneyKeys = {
    mine: (userId: string, householdId: string) => ['personal-money', userId, householdId, 'mine'] as const,
    boundary: (userId: string, householdId: string) => ['statistics', userId, householdId, 'boundary-flow'] as const,
};

export const personalMoneyQueries = {
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

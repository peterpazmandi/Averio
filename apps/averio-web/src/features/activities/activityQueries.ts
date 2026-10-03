import { queryOptions } from '@tanstack/react-query';
import { ActivitiesApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';

export const activityApi = new ActivitiesApi(generatedApiConfiguration);
export const activityKeys = { all: (userId: string, householdId: string) => ['activities', userId, householdId] as const };
export const activityQueries = {
    list: (userId: string, householdId: string) => queryOptions({
        queryKey: activityKeys.all(userId, householdId),
        queryFn: ({ signal }) => activityApi.apiV1HouseholdsHouseholdIdActivitiesGet({ householdId }, { signal }),
    }),
};
export const emptyAssignmentVersion = '00000000-0000-0000-0000-000000000000';

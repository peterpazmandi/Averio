import { queryOptions, useMutation, useQueryClient } from '@tanstack/react-query';
import { CategoriesApi, IconsApi } from '../../shared/api/generated';
import type { CreateCategoryRequest, UpdateCategoryRequest, CategoryResponse } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
import { dashboardQueryKeys } from '../dashboard/api/dashboardQueryKeys';

const api = new CategoriesApi(generatedApiConfiguration);
const icons = new IconsApi(generatedApiConfiguration);
export const categoryKeys = {
    all: (userId: string, householdId: string) => ['categories', userId, householdId] as const,
    management: (userId: string, householdId: string) => [...categoryKeys.all(userId, householdId), 'management'] as const,
};
export const categoryQueries = {
    management: (userId: string, householdId: string) => queryOptions({
        queryKey: categoryKeys.management(userId, householdId),
        queryFn: ({ signal }) => api.apiV1HouseholdsHouseholdIdCategoriesGet({ householdId, includeArchived: true }, { signal }),
    }),
    icons: () => queryOptions({
        queryKey: ['icons'],
        queryFn: ({ signal }) => icons.apiV1IconsGet({ signal }),
        staleTime: 60 * 60 * 1000,
    }),
};
export function useCategoryMutations(userId: string, householdId: string) {
    const client = useQueryClient();
    const invalidate = () => Promise.all([
        client.invalidateQueries({ queryKey: categoryKeys.all(userId, householdId) }),
        client.invalidateQueries({ queryKey: ['statistics', userId, householdId] }),
        client.invalidateQueries({ queryKey: dashboardQueryKeys.household(userId, householdId) }),
    ]);
    return {
        create: useMutation({
            mutationFn: (request: CreateCategoryRequest) => api.apiV1HouseholdsHouseholdIdCategoriesPost({
                householdId, createCategoryRequest: request,
            }),
            onSuccess: invalidate,
        }),
        update: useMutation({
            mutationFn: ({ id, request }: { id: string; request: UpdateCategoryRequest }) =>
                api.apiV1HouseholdsHouseholdIdCategoriesCategoryIdPut({ householdId, categoryId: id, updateCategoryRequest: request }),
            onSuccess: invalidate,
        }),
        archive: useMutation({
            mutationFn: (category: CategoryResponse) => api.apiV1HouseholdsHouseholdIdCategoriesCategoryIdArchivePut({
                householdId, categoryId: category.id,
                categoryArchiveRequest: { isArchived: !category.isArchived, expectedUpdatedAt: category.updatedAt },
            }),
            onSuccess: invalidate,
        }),
    };
}

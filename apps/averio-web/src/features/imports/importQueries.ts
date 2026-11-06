import { queryOptions } from '@tanstack/react-query';
import { importsApi } from './importApi';
export const importQueries = {
    history: (userId: string, householdId: string, page: number) => queryOptions({
        queryKey: ['imports', userId, householdId, 'history', page],
        queryFn: ({ signal }) => importsApi.apiV1HouseholdsHouseholdIdImportsGet({ householdId, page }, { signal }),
    }),
    records: (userId: string, householdId: string, id: string, page: number, review: boolean) => queryOptions({
        queryKey: ['imports', userId, householdId, id, page, review], enabled: !!id,
        queryFn: ({ signal }) => review
            ? importsApi.apiV1HouseholdsHouseholdIdImportsIdUndoPreviewGet({ householdId, id, page }, { signal })
            : importsApi.apiV1HouseholdsHouseholdIdImportsIdRecordsGet({ householdId, id, page }, { signal }),
    }),
};

import { apiFetch } from '../../../shared/api/apiFetch';
import type { DashboardResponse } from '../../../shared/api/generated';

export function getDashboard(
    householdId: string,
    signal?: AbortSignal,
): Promise<DashboardResponse> {
    const id = encodeURIComponent(householdId);

    return apiFetch<DashboardResponse>(
        `/api/v1/households/${id}/dashboard`,
        { signal },
    );
}
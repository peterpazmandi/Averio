import { DashboardApi } from '../../../shared/api/generated';
import type { DashboardResponse } from '../../../shared/api/generated';
import { generatedApiConfiguration } from '../../../shared/api/generatedApiConfiguration';

export type DashboardData = DashboardResponse;

const dashboard = new DashboardApi(generatedApiConfiguration);

export function getDashboard(
    householdId: string,
    signal?: AbortSignal,
): Promise<DashboardData> {
    return dashboard.apiV1HouseholdsHouseholdIdDashboardGet(
        { householdId },
        { signal },
    );
}

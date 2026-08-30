import type {
    SystemHealthResponse,
} from '../../../shared/api/generated/models/SystemHealthResponse';

import {
    apiFetch,
} from '../../../shared/api/apiFetch';

export const getSystemHealth =
    async (): Promise<SystemHealthResponse> => {
        return await apiFetch<SystemHealthResponse>(
            '/api/v1/system/health',
        );
    };
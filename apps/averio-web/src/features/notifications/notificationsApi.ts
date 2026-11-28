import { NotificationsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
export type { NotificationItem } from '../../shared/api/generated';

const api = new NotificationsApi(generatedApiConfiguration);

export const notificationKeys = {
    list: (userId: string, householdId: string) => ['notifications', userId, householdId, 'list'] as const,
    count: (userId: string, householdId: string) => ['notifications', userId, householdId, 'count'] as const,
};

export const notificationsApi = {
    list: (householdId: string) => api.apiV1NotificationsGet({ householdId }),
    count: (householdId: string) => api.apiV1NotificationsUnreadCountGet({ householdId }),
    read: (id: string) => api.apiV1NotificationsIdReadPut({ id }),
    readAll: (householdId: string) => api.apiV1NotificationsReadAllPut({ householdId }),
    dismiss: (id: string) => api.apiV1NotificationsIdDismissPut({ id }),
};

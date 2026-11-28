import { apiFetch } from '../../shared/api/apiFetch';

// View model deliberately excludes financial descriptions and amounts.
export interface NotificationItem {
    id: string;
    householdId: string;
    type: string;
    targetId: string;
    createdAt: string;
    readAt: string | null;
}

export const notificationKeys = {
    list: (userId: string, householdId: string) => ['notifications', userId, householdId, 'list'] as const,
    count: (userId: string, householdId: string) => ['notifications', userId, householdId, 'count'] as const,
};

export const notificationsApi = {
    list: (householdId: string) => apiFetch<NotificationItem[]>(`/api/v1/notifications?householdId=${encodeURIComponent(householdId)}`),
    count: (householdId: string) => apiFetch<{ count: number }>(`/api/v1/notifications/unread-count?householdId=${encodeURIComponent(householdId)}`),
    read: (id: string) => apiFetch<void>(`/api/v1/notifications/${encodeURIComponent(id)}/read`, { method: 'PUT' }),
    readAll: (householdId: string) => apiFetch<void>(`/api/v1/notifications/read-all?householdId=${encodeURIComponent(householdId)}`, { method: 'PUT' }),
    dismiss: (id: string) => apiFetch<void>(`/api/v1/notifications/${encodeURIComponent(id)}/dismiss`, { method: 'PUT' }),
};

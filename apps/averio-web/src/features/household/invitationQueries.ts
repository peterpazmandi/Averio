import { InvitationsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
export const invitationApi = new InvitationsApi(generatedApiConfiguration);
export const invitationKeys = {
    all: ['invitations'] as const,
    list: (userId: string, householdId: string) => ['invitations', userId, householdId] as const,
    pending: (userId: string | undefined) => ['invitations', userId, 'pending'] as const,
    history: (userId: string) => ['invitations', userId, 'history'] as const,
    snapshot: (userId: string, id: string, from: string, to: string) => ['invitations', userId, 'history', id, from, to] as const,
    former: (userId: string, householdId: string) => ['invitations', userId, householdId, 'former'] as const,
};


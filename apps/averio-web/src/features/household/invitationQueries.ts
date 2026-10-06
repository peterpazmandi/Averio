import { InvitationsApi } from '../../shared/api/generated';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';
export const invitationApi = new InvitationsApi(generatedApiConfiguration);
export const invitationKeys = {
    all: ['invitations'] as const,
    list: (userId: string, householdId: string) => ['invitations', userId, householdId] as const,
};

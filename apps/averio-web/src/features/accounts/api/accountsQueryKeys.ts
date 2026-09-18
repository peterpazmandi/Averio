export const accountsQueryKeys = {
    all: ['accounts'] as const,

    household: (userId: string, householdId: string,) => ['accounts', userId, householdId] as const,

    list: (userId: string, householdId: string, includeArchived: boolean) => [
        ...accountsQueryKeys.household(userId, householdId),
        'list',
        includeArchived,
    ] as const,

    ownerOptions: (userId: string, householdId: string) => [
        ...accountsQueryKeys.household(userId, householdId),
        'owner-options',
    ] as const,

    detail: (userId: string, householdId: string, accountId: string) => [
        ...accountsQueryKeys.household(userId, householdId),
        'detail',
        accountId,
    ] as const,
};

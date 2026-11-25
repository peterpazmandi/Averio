export const moneyBackQueryKeys = {
    household: (userId: string, householdId: string) => ['money-back', userId, householdId] as const,
};

export const dashboardQueryKeys = {
    all: ['dashboard'] as const,

    household: (
        userId: string,
        householdId: string,
    ) => [
        ...dashboardQueryKeys.all,
        userId,
        householdId,
    ] as const,
};
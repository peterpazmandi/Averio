export const onboardingQueryKeys = {
    all: ['onboarding'] as const,

    user: (userId: string) =>
        [...onboardingQueryKeys.all, userId] as const,

    status: (userId: string) =>
        [...onboardingQueryKeys.user(userId), 'status'] as const,
};
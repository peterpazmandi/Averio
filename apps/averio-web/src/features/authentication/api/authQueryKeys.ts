export const authQueryKeys = {
    all: ['authentication'] as const,

    currentUser: () => [...authQueryKeys.all, 'current-user'] as const,
};
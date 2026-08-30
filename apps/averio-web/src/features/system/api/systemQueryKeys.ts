export const systemQueryKeys = {
    all: [
        'system',
    ] as const,

    health: () => [
        ...systemQueryKeys.all,
        'health',
    ] as const,
};
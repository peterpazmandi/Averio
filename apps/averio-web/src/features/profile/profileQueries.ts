import { queryOptions } from '@tanstack/react-query';
import { profileApi } from './profileApi';

export const profileKeys = {
    detail: (userId: string) => ['user-profile', userId] as const,
    picture: (userId: string) => ['user-profile-picture', userId] as const,
};

export const profileQueries = {
    detail: (userId: string) => queryOptions({
        queryKey: profileKeys.detail(userId),
        queryFn: ({ signal }) => profileApi.get(signal),
    }),
    picture: (userId: string) => queryOptions({
        queryKey: profileKeys.picture(userId),
        queryFn: async ({ signal }) => {
            const blob = await profileApi.getPicture(signal);
            return await new Promise<string>((resolve, reject) => {
                const reader = new FileReader();
                reader.onload = () => typeof reader.result === 'string' ? resolve(reader.result) : reject(new Error('Picture could not be read.'));
                reader.onerror = () => reject(reader.error ?? new Error('Picture could not be read.'));
                reader.readAsDataURL(blob);
            });
        },
        staleTime: 5 * 60 * 1000,
    }),
};

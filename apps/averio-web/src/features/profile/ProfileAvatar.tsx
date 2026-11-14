import { useQuery } from '@tanstack/react-query';
import { profileQueries } from './profileQueries';

export function ProfileAvatar({ userId, initials, className = '' }: { userId: string; initials: string; className?: string }) {
    const profile = useQuery({ ...profileQueries.detail(userId) });
    const picture = useQuery({ ...profileQueries.picture(userId), enabled: profile.data?.hasProfilePicture === true });
    return picture.data
        ? <img src={picture.data} alt="" aria-hidden="true" className={`${className} object-cover`} />
        : <span className={className} aria-hidden="true">{initials}</span>;
}

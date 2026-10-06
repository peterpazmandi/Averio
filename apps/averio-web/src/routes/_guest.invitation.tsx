import { createFileRoute } from '@tanstack/react-router';
import { InvitationPage } from '../features/household/InvitationPage';
export const Route = createFileRoute('/_guest/invitation')({ component: InvitationPage });

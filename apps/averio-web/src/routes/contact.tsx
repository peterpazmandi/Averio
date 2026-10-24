import { createFileRoute } from '@tanstack/react-router';
import { ContactPage } from '../features/landing/ContactPage';

export const Route = createFileRoute('/contact')({ component: ContactPage });

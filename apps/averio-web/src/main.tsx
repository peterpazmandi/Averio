import {
    StrictMode,
} from 'react';

import {
    createRoot,
} from 'react-dom/client';

import {
    RouterProvider,
} from '@tanstack/react-router';

import './index.css';
import './i18n';

import {
    AppProviders,
} from './app/AppProviders';

import {
    router,
} from './app/router';

const rootElement =
    document.getElementById('root');

if (!rootElement) {
    throw new Error(
        'Root element was not found.',
    );
}

createRoot(rootElement).render(
    <StrictMode>
        <AppProviders>
            <RouterProvider
                router={router}
            />
        </AppProviders>
    </StrictMode>,
);
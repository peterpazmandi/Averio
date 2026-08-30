import {
    StrictMode,
} from 'react';

import {
    createRoot,
} from 'react-dom/client';

import './index.css';
import './i18n';

import App from './App';

import {
    AppProviders,
} from './app/AppProviders';

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
            <App />
        </AppProviders>
    </StrictMode>,
);
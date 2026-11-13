declare global {
    interface Window {
        __AVERIO_CONFIG__?: {
            apiUrl: string;
            environment: 'integration' | 'production';
        };
    }
}

const runtime = window.__AVERIO_CONFIG__;

export const runtimeConfiguration = {
    apiUrl: runtime?.apiUrl ?? import.meta.env.VITE_API_URL ?? 'http://localhost:5180',
    environment: runtime?.environment ?? import.meta.env.MODE,
};

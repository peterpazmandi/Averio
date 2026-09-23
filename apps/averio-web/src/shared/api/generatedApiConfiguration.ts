import { Configuration } from './generated';
import { API_BASE_URL } from './apiConfiguration';
import { apiFetchResponse } from './apiFetch';

export const generatedApiConfiguration = new Configuration({
    basePath: API_BASE_URL,
    fetchApi: (url, init) =>
        apiFetchResponse(String(url).slice(API_BASE_URL.length), init),
    middleware: [{
        onError: async ({ error }) => {
            throw error;
        },
    }],
});

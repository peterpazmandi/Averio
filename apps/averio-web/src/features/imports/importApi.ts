import { ImportsApi, ImportPreviewFromJSON } from '../../shared/api/generated';
import { apiFetchResponse } from '../../shared/api/apiFetch';
import { generatedApiConfiguration } from '../../shared/api/generatedApiConfiguration';

export const importsApi = new ImportsApi(generatedApiConfiguration);

/**
 * Uses the existing generated ImportPreview model. Regenerate ImportsApi when
 * updating the OpenAPI client to expose the new /ynab upload operation.
 */
export async function uploadYnabRegister(householdId: string, file: File,
    currency: string, dateFormat: string, numberFormat: string) {
    const body = new FormData();
    body.append('file', file);
    body.append('currency', currency);
    body.append('dateFormat', dateFormat);
    body.append('numberFormat', numberFormat);
    const response = await apiFetchResponse(`/api/v1/households/${encodeURIComponent(householdId)}/imports/ynab`, {
        method: 'POST', body,
    });
    return ImportPreviewFromJSON(await response.json());
}

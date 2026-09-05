import type { CompleteOnboardingRequest, OnboardingStatusResponse } from '../../../shared/api/generated';
import { apiFetch } from '../../../shared/api/apiFetch';

export async function getOnboardingStatus():
    Promise<OnboardingStatusResponse> {
    return await apiFetch<OnboardingStatusResponse>(
        '/api/v1/onboarding',
    );
}

export async function completeOnboarding(
    request: CompleteOnboardingRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/onboarding',
        {
            method: 'PUT',
            body: JSON.stringify(request),
        },
    );
}
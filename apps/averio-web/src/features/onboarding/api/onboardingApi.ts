import type { CompleteOnboardingRequest, CreateAccountRequest, OnboardingStatusResponse } from '../../../shared/api/generated';
import { apiFetch } from '../../../shared/api/apiFetch';
import type { SaveOnboardingProfileRequest, SaveOnboardingHouseholdRequest } from '../../../shared/api/generated';

export async function getOnboardingStatus(signal?: AbortSignal): Promise<OnboardingStatusResponse> {
    return await apiFetch<OnboardingStatusResponse>(
        '/api/v1/onboarding',
        { signal },
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

export async function saveOnboardingProfile(
    request: SaveOnboardingProfileRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/onboarding/profile',
        {
            method: 'PUT',
            body: JSON.stringify(request),
        },
    );
}

export async function saveOnboardingHousehold(
    request: SaveOnboardingHouseholdRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/onboarding/household',
        {
            method: 'PUT',
            body: JSON.stringify(request),
        },
    );
}

export async function completeFirstAccount(
    householdId: string,
    request: CreateAccountRequest,
): Promise<void> {
    const id = encodeURIComponent(householdId);

    await apiFetch<void>(
        `/api/v1/households/${id}/onboarding/first-account`,
        {
            method: 'PUT',
            body: JSON.stringify(request),
        },
    );
}
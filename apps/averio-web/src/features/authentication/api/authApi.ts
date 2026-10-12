import type {
    CurrentUserResponse,
    LoginUserRequest,
    RegisterUserRequest,
    RegisterUserResponse,
    ResendEmailVerificationRequest,
    VerifyEmailRequest,
    RequestPasswordResetRequest,
    ResetPasswordRequest,
} from '../../../shared/api/generated';
import {
    apiFetch,
} from '../../../shared/api/apiFetch';
import {
    clearCsrfToken,
} from '../../../shared/api/csrf';

export async function registerUser(
    request: RegisterUserRequest,
): Promise<RegisterUserResponse> {
    return await apiFetch<RegisterUserResponse>(
        '/api/v1/auth/register',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function loginUser(
    request: LoginUserRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/login',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );

    /*
     * Authentication identity changed.
     * Antiforgery request token should be refreshed.
     */
    clearCsrfToken();
}

export async function logoutUser(): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/logout',
        {
            method: 'POST',
        },
    );

    clearCsrfToken();
}

export async function verifyEmail(
    request: VerifyEmailRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/email-verification',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function resendEmailVerification(
    request: ResendEmailVerificationRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/email-verification/resend',
        {
            method: 'POST',
            body: JSON.stringify(request),
        },
    );
}

export async function getCurrentUser(signal?: AbortSignal):
    Promise<CurrentUserResponse> {
    return await apiFetch<CurrentUserResponse>(
        '/api/v1/auth/me',
        { signal },
    );
}

export async function requestPasswordReset(
    request: RequestPasswordResetRequest,
): Promise<void> {
    await apiFetch<void>(
        '/api/v1/auth/password-reset-requests',
        {
            method: 'POST',
            body:
                JSON.stringify(
                    request,
                ),
        },
    );
}

export async function resetPassword(request: ResetPasswordRequest): Promise<void> {
    await apiFetch<void>('/api/v1/auth/password-resets', {
            method: 'POST',
            body:
                JSON.stringify(
                    request,
                ),
        },
    );
}

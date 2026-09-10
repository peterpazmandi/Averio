import { useState, type FormEvent } from 'react';
import { Link, createFileRoute } from '@tanstack/react-router';
import {
    useMutation,
} from '@tanstack/react-query';
import {
    useTranslation,
} from 'react-i18next';
import {
    resetPassword,
} from '../features/authentication/api/authApi';
import {
    ApiError,
} from '../shared/api/ApiError';
import { PasswordField } from '../shared/components/PasswordField';
import { PasswordStrengthIndicator } from '../shared/components/PasswordStrengthIndicator';
import { PrimaryButton } from '../shared/components/PrimaryButton';

export const Route = createFileRoute('/_guest/reset-password',)({
    validateSearch: (
        search: Record<string, unknown>,
    ) => ({
        userId:
            typeof search.userId === 'string'
                ? search.userId
                : undefined,

        token:
            typeof search.token === 'string'
                ? search.token
                : undefined,
    }),

    component:
        ResetPasswordPage,
});

function ResetPasswordPage() {
    const { t } = useTranslation();

    const search = Route.useSearch();

    const userId = search.userId;

    const token = search.token;

    const [password, setPassword] = useState('');

    const [confirmPassword, setConfirmPassword] = useState('');

    const [submitted, setSubmitted] = useState(false);

    const mutation =
        useMutation({
            mutationFn:
                resetPassword,
        });

    const linkIsInvalid = !userId || !token;
    const passwordMismatch = confirmPassword.length > 0 && password !== confirmPassword;
    const passwordTooShort = submitted && password.length < 12;
    const confirmPasswordRequired = submitted && confirmPassword.length === 0;

    const canSubmit =
        !linkIsInvalid
        && password.length >= 12
        && password.length <= 128
        && confirmPassword.length > 0
        && password === confirmPassword
        && !mutation.isPending;

    async function submit(
        event: FormEvent<HTMLFormElement>,
    ) {
        event.preventDefault();

        setSubmitted(true);

        if (
            !userId
            || !token
            || password.length < 12
            || password.length > 128
            || !confirmPassword
            || password !== confirmPassword
        ) {
            return;
        }

        try {
            await mutation.mutateAsync({
                userId,
                token,
                password,
            });
        }
        catch {
            /*
             * Error rendered via mutation state.
             */
        }
    }

    /*
     * The user opened /reset-password without
     * the userId/token from the email.
     */
    if (linkIsInvalid) {
        return (
            <InvalidResetLinkState />
        );
    }

    /*
     * Password reset completed.
     */
    if (mutation.isSuccess) {
        return (
            <ResetPasswordSuccessState />
        );
    }

    const apiError =
        mutation.error
            instanceof ApiError
            ? mutation.error
            : undefined;

    const tokenInvalid =
        apiError?.code
        === 'invalid_password_reset_token';

    if (tokenInvalid) {
        return (
            <InvalidResetLinkState />
        );
    }

    const passwordError =
        passwordTooShort
            ? t(
                'password.validation.minimumLength',
            )
            : apiError?.validationErrors
                ?.password
                ?.at(0);

    const confirmPasswordError =
        confirmPasswordRequired
            ? t(
                'password.validation.confirmRequired',
            )
            : passwordMismatch
                ? t(
                    'password.mismatch',
                )
                : undefined;

    return (
        <div>
            <div
                className="
                    inline-flex
                    size-14
                    items-center
                    justify-center
                    rounded-2xl
                    bg-primary-100
                    text-primary-800

                    dark:bg-primary-950
                    dark:text-primary-300
                "
            >
                <LockIcon />
            </div>

            <div className="mt-6">
                <p
                    className="
                        text-sm
                        font-semibold
                        text-primary-700

                        dark:text-primary-400
                    "
                >
                    {t(
                        'auth.reset.eyebrow',
                    )}
                </p>

                <h1
                    className="
                        mt-2
                        text-3xl
                        font-semibold
                        tracking-tight
                        text-slate-950

                        dark:text-white
                    "
                >
                    {t(
                        'auth.reset.title',
                    )}
                </h1>

                <p
                    className="
                        mt-3
                        leading-7
                        text-slate-600

                        dark:text-slate-400
                    "
                >
                    {t(
                        'auth.reset.description',
                    )}
                </p>
            </div>

            <form
                onSubmit={submit}
                noValidate
                className="
                    mt-8
                    space-y-5
                "
            >
                <div>
                    <PasswordField
                        label={t(
                            'auth.password',
                        )}
                        autoComplete="new-password"
                        required
                        minLength={12}
                        maxLength={128}
                        value={password}
                        error={
                            passwordError
                        }
                        onChange={event => {
                            setPassword(
                                event.target.value,
                            );
                        }}
                    />

                    <PasswordStrengthIndicator
                        password={password}
                    />

                    <p
                        className="
                            mt-2
                            text-xs
                            leading-5
                            text-slate-500

                            dark:text-slate-400
                        "
                    >
                        {t(
                            'password.requirements',
                        )}
                    </p>
                </div>

                <PasswordField
                    label={t(
                        'auth.confirmPassword',
                    )}
                    autoComplete="new-password"
                    required
                    maxLength={128}
                    value={confirmPassword}
                    error={
                        confirmPasswordError
                    }
                    onChange={event => {
                        setConfirmPassword(
                            event.target.value,
                        );
                    }}
                />

                {apiError && (
                    <ApiErrorMessage
                        error={apiError}
                    />
                )}

                <PrimaryButton
                    type="submit"
                    loading={
                        mutation.isPending
                    }
                    disabled={
                        !canSubmit
                    }
                >
                    {t(
                        'auth.reset.submit',
                    )}
                </PrimaryButton>
            </form>

            <div
                className="
                    mt-6
                    text-center
                "
            >
                <Link
                    to="/login"
                    search={{
                        redirect: undefined,
                    }}
                    className="
                        text-sm
                        font-semibold
                        text-primary-700
                        transition
                        hover:text-primary-800

                        dark:text-primary-400
                        dark:hover:text-primary-300
                    "
                >
                    {t(
                        'auth.forgot.backToLogin',
                    )}
                </Link>
            </div>
        </div>
    );
}

function ApiErrorMessage({
    error,
}: {
    error: ApiError;
}) {
    const {
        t,
    } = useTranslation();

    /*
     * Password policy rejection can be returned
     * as a business-rule error instead of a
     * ValidationProblemDetails response.
     */
    const message =
        error.code === 'password_rejected'
            ? t(
                'password.validation.rejected',
            )
            : error.message;

    return (
        <div
            role="alert"
            className="
                rounded-2xl
                border
                border-red-200
                bg-red-50
                p-4

                dark:border-red-900/50
                dark:bg-red-950/40
            "
        >
            <div
                className="
                    flex
                    items-start
                    gap-3
                "
            >
                <div
                    className="
                        mt-0.5
                        flex
                        size-8
                        shrink-0
                        items-center
                        justify-center
                        rounded-lg
                        bg-red-100
                        text-red-700

                        dark:bg-red-900/50
                        dark:text-red-300
                    "
                >
                    !
                </div>

                <div>
                    <p
                        className="
                            text-sm
                            font-semibold
                            text-red-800

                            dark:text-red-300
                        "
                    >
                        {t(
                            'common.requestFailed',
                        )}
                    </p>

                    <p
                        className="
                            mt-1
                            text-sm
                            leading-6
                            text-red-700

                            dark:text-red-400
                        "
                    >
                        {message}
                    </p>

                    {error.traceId && (
                        <p
                            className="
                                mt-2
                                text-xs
                                text-red-500

                                dark:text-red-500
                            "
                        >
                            {t(
                                'common.reference',
                                {
                                    reference:
                                        error.traceId,
                                },
                            )}
                        </p>
                    )}
                </div>
            </div>
        </div>
    );
}

function ResetPasswordSuccessState() {
    const {
        t,
    } = useTranslation();

    return (
        <div>
            <div
                className="
                    flex
                    size-14
                    items-center
                    justify-center
                    rounded-full
                    bg-primary-100
                    text-primary-800

                    dark:bg-primary-950
                    dark:text-primary-300
                "
            >
                <CheckIcon />
            </div>

            <h1
                className="
                    mt-6
                    text-3xl
                    font-semibold
                    tracking-tight
                    text-slate-950

                    dark:text-white
                "
            >
                {t(
                    'auth.reset.successTitle',
                )}
            </h1>

            <p
                className="
                    mt-3
                    leading-7
                    text-slate-600

                    dark:text-slate-400
                "
            >
                {t(
                    'auth.reset.successDescription',
                )}
            </p>

            <Link
                to="/login"
                search={{
                    redirect: undefined,
                }}
                className="
                    mt-8
                    inline-flex
                    w-full
                    items-center
                    justify-center
                    rounded-xl
                    bg-primary-500
                    px-5
                    py-3
                    font-semibold
                    text-slate-950
                    transition

                    hover:bg-primary-400

                    focus:outline-none
                    focus:ring-4
                    focus:ring-primary-200

                    dark:focus:ring-primary-900
                "
            >
                {t(
                    'auth.login.submit',
                )}
            </Link>
        </div>
    );
}

function InvalidResetLinkState() {
    const {
        t,
    } = useTranslation();

    return (
        <div>
            <div
                className="
                    flex
                    size-14
                    items-center
                    justify-center
                    rounded-2xl
                    bg-amber-100
                    text-amber-700

                    dark:bg-amber-950/60
                    dark:text-amber-300
                "
            >
                <BrokenLinkIcon />
            </div>

            <h1
                className="
                    mt-6
                    text-3xl
                    font-semibold
                    tracking-tight
                    text-slate-950

                    dark:text-white
                "
            >
                {t('auth.reset.invalidTitle')}
            </h1>

            <p
                className="
                    mt-3
                    leading-7
                    text-slate-600

                    dark:text-slate-400
                "
            >
                {t('auth.reset.invalidDescription')}
            </p>

            <Link
                to="/forgot-password"
                className="
                    mt-8
                    inline-flex
                    w-full
                    items-center
                    justify-center
                    rounded-xl
                    bg-primary-500
                    px-5
                    py-3
                    font-semibold
                    text-slate-950
                    transition

                    hover:bg-primary-400

                    focus:outline-none
                    focus:ring-4
                    focus:ring-primary-200

                    dark:focus:ring-primary-900
                "
            >
                {t('auth.reset.requestAnother')}
            </Link>

            <Link
                to="/login"
                search={{
                    redirect: undefined,
                }}
                className="
                    mt-4
                    block
                    text-center
                    text-sm
                    font-semibold
                    text-primary-700
                    transition
                    hover:text-primary-800

                    dark:text-primary-400
                    dark:hover:text-primary-300
                "
            >
                {t('auth.forgot.backToLogin')}
            </Link>
        </div>
    );
}

function LockIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <rect
                x="5"
                y="10"
                width="14"
                height="10"
                rx="2"
            />

            <path
                d="
                    M8 10
                    V7
                    a4 4 0 0 1
                    8 0
                    v3
                "
            />
        </svg>
    );
}

function CheckIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2.2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <path
                d="
                    m5 12
                    4 4
                    L19 6
                "
            />
        </svg>
    );
}

function BrokenLinkIcon() {
    return (
        <svg
            viewBox="0 0 24 24"
            className="size-6"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            strokeLinejoin="round"
            aria-hidden="true"
        >
            <path
                d="
                    M9.5 14.5
                    14.5 9.5
                "
            />

            <path
                d="
                    M7.2 17.8
                    5.6 19.4
                    a3.5 3.5 0 0 1
                    -5 -5
                    l3.5 -3.5
                    a3.5 3.5 0 0 1
                    5 0
                "
            />

            <path
                d="
                    M16.8 6.2
                    18.4 4.6
                    a3.5 3.5 0 0 1
                    5 5
                    l-3.5 3.5
                    a3.5 3.5 0 0 1
                    -5 0
                "
            />
        </svg>
    );
}
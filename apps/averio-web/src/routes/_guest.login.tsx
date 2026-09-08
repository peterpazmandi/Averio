import {
    useState,
    type FormEvent,
} from 'react';

import {
    Link,
    createFileRoute,
    useNavigate,
} from '@tanstack/react-router';

import {
    useMutation,
} from '@tanstack/react-query';

import {
    useTranslation,
} from 'react-i18next';

import {
    loginUser,
} from '../features/authentication/api/authApi';

import {
    useAuth,
} from '../features/authentication/context/AuthProvider';

import {
    ApiError,
} from '../shared/api/ApiError';

import {
    FormField,
} from '../shared/components/FormField';

import {
    PrimaryButton,
} from '../shared/components/PrimaryButton';
import { PasswordField } from '../shared/components/PasswordField';

export const Route =
    createFileRoute('/_guest/login')({
        validateSearch:
            (
                search:
                    Record<string, unknown>,
            ) => ({
                redirect:
                    typeof search.redirect
                        === 'string'
                        ? search.redirect
                        : undefined,
            }),

        component:
            LoginPage,
    });

function LoginPage() {
    const { t } =
        useTranslation();

    const search =
        Route.useSearch();

    const navigate =
        useNavigate();

    const auth =
        useAuth();

    const [email, setEmail] =
        useState('');

    const [password, setPassword] =
        useState('');

    const [rememberMe, setRememberMe] =
        useState(true);

    const mutation =
        useMutation({
            mutationFn:
                loginUser,
        });

    async function submit(
        event: FormEvent,
    ) {
        event.preventDefault();

        try {
            await mutation.mutateAsync({
                email,
                password,
                rememberMe,
            });

            const user =
                await auth.refresh();

            if (user.onboardingRequired) {
                await navigate({
                    to: '/onboarding',
                });

                return;
            }

            const redirect =
                search.redirect?.startsWith('/')
                    ? search.redirect
                    : '/app';

            await navigate({
                to: redirect,
            });
        }
        catch {
            // Mutation state renders error.
        }
    }

    const error =
        mutation.error instanceof ApiError
            ? mutation.error
            : undefined;

    return (
        <>
            <p
                className="
                    text-sm
                    font-semibold
                    text-primary-700
                "
            >
                {t('auth.login.eyebrow')}
            </p>

            <h1
                className="
                    mt-2
                    text-3xl
                    font-semibold
                    tracking-tight
                "
            >
                {t('auth.login.title')}
            </h1>

            <p
                className="
                    mt-3
                    text-slate-600
                "
            >
                {t('auth.login.description')}
            </p>

            <form
                onSubmit={submit}
                className="
                    mt-8
                    space-y-5
                "
            >
                <FormField
                    label={t('auth.email')}
                    type="email"
                    autoComplete="email"
                    required
                    value={email}
                    onChange={(event) =>
                        setEmail(
                            event.target.value,
                        )
                    }
                />

                <PasswordField
                    label={t('auth.password')}
                    autoComplete="current-password"
                    required
                    value={password}
                    onChange={event =>
                        setPassword(
                            event.target.value,
                        )
                    }
                />

                <div
                    className="
                        flex
                        min-w-0
                        justify-end
                    "
                >
                    <Link
                        to="/forgot-password"
                        className="
                            min-w-0
                            text-right

                            text-sm
                            font-semibold

                            text-primary-700

                            hover:text-primary-800

                            dark:text-primary-400
                            dark:hover:text-primary-300
                        "
                    >
                        {t(
                            'auth.login.forgotPassword',
                        )}
                    </Link>
                </div>

                <label
                    className="
                        flex
                        items-center
                        gap-3
                        text-sm
                        text-slate-600
                    "
                >
                    <input
                        type="checkbox"
                        checked={rememberMe}
                        onChange={(event) =>
                            setRememberMe(
                                event.target.checked,
                            )
                        }
                        className="
                            size-4
                            accent-primary-500
                        "
                    />

                    {t('auth.login.rememberMe')}
                </label>

                {error && (
                    <div
                        className="
                            rounded-xl
                            bg-red-50
                            p-4
                            text-sm
                            text-red-700
                        "
                    >
                        {error.message}

                        {error.code
                            === 'email_not_verified'
                            && (
                                <div className="mt-2">
                                    <Link
                                        to="/resend-verification"
                                        className="
                                            font-semibold
                                            underline
                                        "
                                    >
                                        {t(
                                            'auth.resend.link',
                                        )}
                                    </Link>
                                </div>
                            )}
                    </div>
                )}

                <PrimaryButton
                    type="submit"
                    loading={mutation.isPending}
                >
                    {t('auth.login.submit')}
                </PrimaryButton>
            </form>

            <p
                className="
                    mt-8
                    text-center
                    text-sm
                    text-slate-600
                "
            >
                {t('auth.login.noAccount')}{' '}

                <Link
                    to="/register"
                    className="
                        font-semibold
                        text-primary-700
                    "
                >
                    {t('auth.register.submit')}
                </Link>
            </p>
        </>
    );
}
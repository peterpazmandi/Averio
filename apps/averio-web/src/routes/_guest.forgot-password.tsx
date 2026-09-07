import {
    useState,
    type FormEvent,
} from 'react';

import {
    Link,
    createFileRoute,
} from '@tanstack/react-router';

import {
    useMutation,
} from '@tanstack/react-query';

import {
    useTranslation,
} from 'react-i18next';

import {
    requestPasswordReset,
} from '../features/authentication/api/authApi';

import {
    FormField,
} from '../shared/components/FormField';

import {
    PrimaryButton,
} from '../shared/components/PrimaryButton';

export const Route = createFileRoute('/_guest/forgot-password')({
    component:
        ForgotPasswordPage,
});

function ForgotPasswordPage() {
    const { t } =
        useTranslation();

    const [email, setEmail] =
        useState('');

    const mutation =
        useMutation({
            mutationFn:
                requestPasswordReset,
        });

    async function submit(
        event: FormEvent,
    ) {
        event.preventDefault();

        try {
            await mutation.mutateAsync({
                email,
            });
        }
        catch {
            // Rendered from mutation state.
        }
    }

    if (mutation.isSuccess) {
        return (
            <>
                <div
                    className="
                        flex size-14
                        items-center
                        justify-center
                        rounded-2xl
                        bg-primary-100
                        text-xl
                        text-primary-800
                        dark:bg-primary-950
                        dark:text-primary-300
                    "
                >
                    @
                </div>

                <h1
                    className="
                        mt-6 text-3xl
                        font-semibold
                        tracking-tight
                        dark:text-white
                    "
                >
                    {t(
                        'auth.forgot.sentTitle',
                    )}
                </h1>

                <p
                    className="
                        mt-3 leading-7
                        text-slate-600
                        dark:text-slate-400
                    "
                >
                    {t(
                        'auth.forgot.sentDescription',
                    )}
                </p>

                <Link
                    to="/login"
                    search={{
                        redirect: undefined,
                    }}
                    className="
                        mt-8 inline-block
                        font-semibold
                        text-primary-700
                        dark:text-primary-400
                    "
                >
                    {t(
                        'auth.forgot.backToLogin',
                    )}
                </Link>
            </>
        );
    }

    return (
        <>
            <p
                className="
                    text-sm font-semibold
                    text-primary-700
                    dark:text-primary-400
                "
            >
                {t(
                    'auth.forgot.eyebrow',
                )}
            </p>

            <h1
                className="
                    mt-2 text-3xl
                    font-semibold
                    tracking-tight
                    dark:text-white
                "
            >
                {t(
                    'auth.forgot.title',
                )}
            </h1>

            <p
                className="
                    mt-3 text-slate-600
                    dark:text-slate-400
                "
            >
                {t(
                    'auth.forgot.description',
                )}
            </p>

            <form
                onSubmit={submit}
                className="mt-8 space-y-5"
            >
                <FormField
                    label={t(
                        'auth.email',
                    )}
                    type="email"
                    required
                    autoComplete="email"
                    value={email}
                    onChange={event =>
                        setEmail(
                            event.target.value,
                        )
                    }
                />

                <PrimaryButton
                    type="submit"
                    loading={
                        mutation.isPending
                    }
                >
                    {t(
                        'auth.forgot.submit',
                    )}
                </PrimaryButton>
            </form>

            <Link
                to="/login"
                search={{
                    redirect: undefined,
                }}
                className="
                    mt-6 block
                    text-center
                    text-sm font-semibold
                    text-primary-700
                    dark:text-primary-400
                "
            >
                {t(
                    'auth.forgot.backToLogin',
                )}
            </Link>
        </>
    );
}
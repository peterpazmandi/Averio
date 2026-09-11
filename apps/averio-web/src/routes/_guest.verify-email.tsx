import {
	useEffect,
	useRef,
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
	verifyEmail,
} from '../features/authentication/api/authApi';

export const Route =
	createFileRoute('/_guest/verify-email')({
		validateSearch:
			(
				search: Record<string, unknown>,
			) => ({
				email:
					typeof search.email
						=== 'string'
						? search.email
						: undefined,

				userId:
					typeof search.userId
						=== 'string'
						? search.userId
						: undefined,

				token:
					typeof search.token
						=== 'string'
						? search.token
						: undefined,
			}),

		component:
			VerifyEmailPage,
	});

function VerifyEmailPage() {
	const { t } = useTranslation();

	const search = Route.useSearch();

	const checkEmailDescription = search.email
		? t('auth.verify.checkEmailDescription', { email: search.email })
		: t('auth.verify.checkEmailDescriptionWithoutEmail');

	const attempted =
		useRef(false);

	const mutation =
		useMutation({
			mutationFn:
				verifyEmail,
		});

	useEffect(
		() => {
			if (
				attempted.current
				|| !search.userId
				|| !search.token
			) {
				return;
			}

			attempted.current = true;

			mutation.mutate({
				userId:
					search.userId,

				token:
					search.token,
			});
		},
		[
			search.userId,
			search.token,
		],
	);

	if (!search.userId || !search.token) {
		return (
			<>
				<EmailIcon />

				<h1
					className="
                        mt-6
                        text-3xl
                        font-semibold
                    "
				>
					{t('auth.verify.checkEmailTitle')}
				</h1>

				<p className="
                        mt-3
                        leading-7
                        text-slate-600
                    "
				>
					{checkEmailDescription}
				</p>


				<Link
					to="/resend-verification"
					className="
                        mt-8
                        inline-block
                        font-semibold
                        text-primary-700
                    "
				>
					{t('auth.resend.link')}
				</Link>
			</>
		);
	}

	if (mutation.isPending) {
		return (
			<p>
				{t('auth.verify.verifying')}
			</p>
		);
	}

	if (mutation.isSuccess) {
		return (
			<>
				<div
					className="
                        flex
                        size-14
                        items-center
                        justify-center
                        rounded-full
                        bg-primary-100
                        text-2xl
                        text-primary-800
                    "
				>
					✓
				</div>

				<h1
					className="
                        mt-6
                        text-3xl
                        font-semibold
                    "
				>
					{t(
						'auth.verify.successTitle',
					)}
				</h1>

				<p
					className="
                        mt-3
                        text-slate-600
                    "
				>
					{t(
						'auth.verify.successDescription',
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
                        rounded-xl
                        bg-primary-500
                        px-5
                        py-3
                        font-semibold
                        text-slate-950
                    "
				>
					{t('auth.login.submit')}
				</Link>
			</>
		);
	}

	if (mutation.isError) {
		return (
			<>
				<h1
					className="
                        text-3xl
                        font-semibold
                    "
				>
					{t(
						'auth.verify.failedTitle',
					)}
				</h1>

				<p
					className="
                        mt-3
                        text-slate-600
                    "
				>
					{t(
						'auth.verify.failedDescription',
					)}
				</p>

				<Link
					to="/resend-verification"
					className="
                        mt-8
                        inline-block
                        font-semibold
                        text-primary-700
                    "
				>
					{t('auth.resend.link')}
				</Link>
			</>
		);
	}

	return null;
}

function EmailIcon() {
	return (
		<div
			className="
                flex
                size-14
                items-center
                justify-center
                rounded-2xl
                bg-primary-100
                text-xl
                text-primary-800
            "
		>
			@
		</div>
	);
}
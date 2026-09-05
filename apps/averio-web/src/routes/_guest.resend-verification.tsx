import {
	useState,
	type FormEvent,
} from 'react'

import {
	Link,
	createFileRoute,
} from '@tanstack/react-router'

import {
	useMutation,
} from '@tanstack/react-query'

import {
	useTranslation,
} from 'react-i18next'

import {
	resendEmailVerification,
} from '../features/authentication/api/authApi'

import {
	ApiError,
} from '../shared/api/ApiError'

import {
	FormField,
} from '../shared/components/FormField'

import {
	PrimaryButton,
} from '../shared/components/PrimaryButton'

export const Route =
	createFileRoute('/_guest/resend-verification')({
		component: ResendVerificationPage,
	})

function ResendVerificationPage() {
	const { t } = useTranslation()

	const [email, setEmail] =
		useState('')

	const [submitted, setSubmitted] =
		useState(false)

	const mutation =
		useMutation({
			mutationFn: resendEmailVerification,
		})

	async function submit(
		event: FormEvent<HTMLFormElement>,
	) {
		event.preventDefault()

		try {
			await mutation.mutateAsync({
				email,
			})

			setSubmitted(true)
		}
		catch {
			// The mutation state renders the error below.
		}
	}

	if (submitted) {
		return (
			<>
				<div
					className="
            flex
            size-14
            items-center
            justify-center
            rounded-2xl
            bg-primary-100
            text-2xl
            text-primary-800
          "
				>
					@
				</div>

				<h1
					className="
            mt-6
            text-3xl
            font-semibold
            tracking-tight
          "
				>
					{t('auth.resend.successTitle')}
				</h1>

				<p
					className="
            mt-3
            leading-7
            text-slate-600
          "
				>
					{t('auth.resend.successDescription')}
				</p>

				<Link
					to="/login"
					search={{
						redirect: undefined,
					}}
					className="
						mt-8
						inline-flex
						font-semibold
						text-primary-700
						transition
						hover:text-primary-800
					"
				>
					← {t('auth.resend.backToLogin')}
				</Link>
			</>
		)
	}

	const error =
		mutation.error instanceof ApiError
			? mutation.error.message
			: mutation.error instanceof Error
				? mutation.error.message
				: undefined

	return (
		<>
			<div
				className="
          flex
          size-14
          items-center
          justify-center
          rounded-2xl
          bg-primary-100
          text-xl
          font-semibold
          text-primary-800
        "
			>
				@
			</div>

			<p
				className="
          mt-6
          text-sm
          font-semibold
          text-primary-700
        "
			>
				{t('auth.resend.eyebrow')}
			</p>

			<h1
				className="
          mt-2
          text-3xl
          font-semibold
          tracking-tight
        "
			>
				{t('auth.resend.title')}
			</h1>

			<p
				className="
          mt-3
          leading-7
          text-slate-600
        "
			>
				{t('auth.resend.description')}
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
						setEmail(event.target.value)
					}
				/>

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
						{error}
					</div>
				)}

				<PrimaryButton
					type="submit"
					loading={mutation.isPending}
				>
					{t('auth.resend.submit')}
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
				<Link
					to="/login"
					search={{
						redirect: undefined,
					}}
					className="
						font-semibold
						text-primary-700
						transition
						hover:text-primary-800
					"
				>
					← {t('auth.resend.backToLogin')}
				</Link>
			</p>
		</>
	)
}
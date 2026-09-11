import { useState, type FormEvent } from 'react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { registerUser } from '../features/authentication/api/authApi';
import { ApiError } from '../shared/api/ApiError';
import { FormField } from '../shared/components/FormField';
import { PrimaryButton } from '../shared/components/PrimaryButton';
import { PasswordField } from '../shared/components/PasswordField';
import { PasswordStrengthIndicator } from '../shared/components/PasswordStrengthIndicator';

export const Route =
	createFileRoute('/_guest/register')({
		component:
			RegisterPage,
	});

function RegisterPage() {
	const { t } = useTranslation();
	const navigate = useNavigate();
	const [email, setEmail] = useState('');
	const [password, setPassword] = useState('');
	const [confirmPassword, setConfirmPassword] = useState('');
	const [submitError, setSubmitError] = useState<unknown>(null);
	const [isSubmitting, setIsSubmitting] = useState(false);

	const passwordMismatch = confirmPassword.length > 0 && password !== confirmPassword;

	const mutation =
		useMutation({
			mutationFn:
				registerUser,
		});

	async function submit(event: FormEvent<HTMLFormElement>) {
		event.preventDefault();

		if (passwordMismatch || isSubmitting) {
			return;
		}

		setSubmitError(null);
		setIsSubmitting(true);

		try {
			await mutation.mutateAsync({ email, password });

			await navigate({
				to: '/verify-email',
				search: {
					email,
					userId: undefined,
					token: undefined,
				},
			});
		} catch (error) {
			setSubmitError(error);
		} finally {
			setIsSubmitting(false);
		}
	}

	const apiError = submitError instanceof ApiError
		? submitError
		: undefined;

	const errorMessage = submitError
		? apiError?.message ?? t('errors.genericDescription')
		: undefined;

	return (
		<>
			<div>
				<p className="text-sm font-semibold text-primary-700" >
					{t('auth.register.eyebrow')}
				</p>

				<h1 className="mt-2 text-3xl font-semibold tracking-tight" >
					{t('auth.register.title')}
				</h1>

				<p className="mt-3 text-slate-600">
					{t('auth.register.description')}
				</p>
			</div>

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
					autoComplete="new-password"
					required
					minLength={12}
					maxLength={128}
					value={password}
					onChange={event =>
						setPassword(
							event.target.value,
						)
					}
				/>
				<PasswordStrengthIndicator password={password} />

				<PasswordField
					label={t(
						'auth.confirmPassword',
					)}
					autoComplete="new-password"
					required
					value={confirmPassword}
					error={
						passwordMismatch
							? t(
								'password.mismatch',
							)
							: undefined
					}
					onChange={event =>
						setConfirmPassword(
							event.target.value,
						)
					}
				/>

				<p className="text-sm text-slate-500" >
					{t('auth.register.passwordHint')}
				</p>

				{errorMessage && (
					<div className="rounded-xl bg-red-50 p-4 text-sm text-red-700">
						{errorMessage}
					</div>
				)}

				<PrimaryButton
					type="submit"
					loading={isSubmitting}
					disabled={passwordMismatch}
				>
					{isSubmitting
						? t('auth.register.submitting')
						: t('auth.register.submit')}
				</PrimaryButton>
			</form>

			<p className="mt-8 text-center text-sm text-slate-600" >
				{t('auth.register.hasAccount')}{' '}

				<Link
					to="/login"
					search={{
						redirect: undefined,
					}}
					className="font-semibold text-primary-700 hover:text-primary-800"
				>
					{t('auth.login.submit')}
				</Link>
			</p>
		</>
	);
}
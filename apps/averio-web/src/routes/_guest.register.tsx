import { useState, type FormEvent } from 'react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useMutation } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { registerUser } from '../features/authentication/api/authApi';
import { ApiError } from '../shared/api/ApiError';
import { FormField } from '../shared/components/FormField';
import { PrimaryButton } from '../shared/components/PrimaryButton';

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

	const mutation =
		useMutation({
			mutationFn:
				registerUser,
		});

	async function submit(event: FormEvent) {
		event.preventDefault();

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
		}
		catch {
			// Mutation state renders the error.
		}
	}

	const error =
		mutation.error instanceof ApiError
			? mutation.error.message
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

				<FormField
					label={t('auth.password')}
					type="password"
					autoComplete="new-password"
					required
					minLength={12}
					value={password}
					onChange={(event) =>
						setPassword(
							event.target.value,
						)
					}
				/>

				<p className="text-sm text-slate-500" >
					{t('auth.register.passwordHint')}
				</p>

				{error && (
					<div className="rounded-xl bg-red-50 p-4 text-sm text-red-700" >
						{error}
					</div>
				)}

				<PrimaryButton
					type="submit"
					loading={mutation.isPending}
				>
					{t('auth.register.submit')}
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
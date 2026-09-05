export const en = {
    app: {
        name: 'Averio',
        description: 'Household finance, clearly understood.',
    },

    system: {
        title: 'System status',
        api: 'API',
        database: 'Database',
        connected: 'Connected',
        unavailable: 'Unavailable',
        loading: 'Checking system status…',
        error: 'The Averio API could not be reached.',
        checkedAt: 'Checked {{date}}',
    },

    authShell: {
        eyebrow:
            'Household finance, together',
        title:
            'Money makes more sense when your household does too.',
        description:
            'Plan shared finances while keeping the flexibility each person needs.',
        footer:
            'Averio — household finance made clear.',
    },

    preferences: {
        theme: {
            light: 'Light mode',
            dark: 'Dark mode',
        },

        language: {
            label: 'Language',
            en: 'English',
            de: 'German',
            hu: 'Hungarian',
        },
    },

    auth: {
        email: 'Email',
        password: 'Password',
        confirmPassword:
            'Confirm password',

        register: {
            eyebrow:
                'Create your Averio account',
            title:
                'Start with your household',
            description:
                'Keep Averio personal for now or invite someone later.',
            passwordHint:
                'Use at least 12 characters.',
            submit:
                'Create account',
            hasAccount:
                'Already have an account?',
        },

        login: {
            eyebrow:
                'Welcome back',
            title:
                'Sign in to Averio',
            description:
                'Continue where your household left off.',
            rememberMe:
                'Keep me signed in',
            forgotPassword:
                'Forgot password?',
            submit:
                'Sign in',
            noAccount:
                "Don't have an account?",
        },

        forgot: {
            eyebrow:
                'Account recovery',
            title:
                'Forgot your password?',
            description:
                "Enter your email and we'll send you a link to choose a new password.",
            submit:
                'Send reset link',
            sentTitle:
                'Check your email',
            sentDescription:
                "If an Averio account exists for that email, we've sent a password reset link.",
            backToLogin:
                'Back to sign in',
        },

        reset: {
            eyebrow:
                'Account recovery',
            title:
                'Choose a new password',
            description:
                'Create a new password for your Averio account.',
            submit:
                'Save new password',
            successTitle:
                'Password changed',
            successDescription:
                'Your password has been updated. You can now sign in.',
            invalidTitle:
                'This link no longer works',
            invalidDescription:
                'The password reset link is invalid or has expired.',
        },

        verify: {
            checkEmailTitle:
                'Check your email',
            checkEmailDescription:
                'We sent you a verification link. Open it to activate your Averio account.',
            verifying:
                'Verifying your email…',
            successTitle:
                'Email verified',
            successDescription:
                'Your Averio account is ready. You can now sign in.',
            failedTitle:
                'That link no longer works',
            failedDescription:
                'The verification link is invalid or has expired.',
        },

        resend: {
            link:
                'Send another verification email',
            title:
                'Send another verification email',
            description:
                'Enter your email address and we will send another verification link.',
            submit:
                'Send verification email',
            sent:
                'If an unverified Averio account exists for this email, a new link has been sent.',
        },
    },

    password: {
        show: 'Show password',
        hide: 'Hide password',

        mismatch:
            'The passwords do not match.',

        strength: {
            empty:
                '',
            weak:
                'Weak password',
            fair:
                'Fair password',
            strong:
                'Strong password',
            veryStrong:
                'Very strong password',
        },
    },

    onboarding: {
        eyebrow: 'One quick setup',
        title: 'Make Averio yours',
        description:
            'Tell us who you are and choose the basic settings for your household.',
        householdOfOne:
            'You are starting with a household of one. Later, you can invite a partner without rebuilding your finances.',
        displayName: 'What should we call you?',
        householdName: 'Household name',
        country: 'Country code',
        currency: 'Preferred currency',
        continue: 'Continue to Averio',
    },

    common: {
        unexpectedError:
            'Something went wrong. Please try again.',
    },

    language: {
        label: 'Language',
        en: 'English',
        de: 'Deutsch',
        hu: 'Magyar',
    },
} as const;
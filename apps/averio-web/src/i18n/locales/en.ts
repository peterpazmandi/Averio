export const en = {
    common: {
        appName: 'Averio',

        actions: {
            continue: 'Continue',
            back: 'Back',
            next: 'Next',
            save: 'Save',
            saving: 'Saving...',
            cancel: 'Cancel',
            close: 'Close',
            done: 'Done',
            edit: 'Edit',
            delete: 'Delete',
            remove: 'Remove',
            retry: 'Try again',
            confirm: 'Confirm',
            skip: 'Skip for now',
            signOut: 'Sign out',
        },

        status: {
            loading: 'Loading...',
            saving: 'Saving...',
            success: 'Success',
            error: 'Something went wrong',
        },

        yes: 'Yes',
        no: 'No',
        optional: 'Optional',
        required: 'Required',

        language: {
            label: 'Language',
            english: 'English',
            german: 'German',
            hungarian: 'Hungarian',
        },

        theme: {
            label: 'Theme',
            light: 'Light',
            dark: 'Dark',
            system: 'System',
            switchToLight: 'Switch to light mode',
            switchToDark: 'Switch to dark mode',
        },
    },

    authShell: {
        eyebrow: 'Household finances made clearer',
        title: 'One place for the money you manage together.',
        description:
            'Averio helps you organize accounts, transactions, goals and household finances without losing sight of your personal money.',
        footer: 'Your finances. Your household. Your way.',
    },

    password: {
        mismatch: 'The passwords do not match.',
        strengthLabel: 'Password strength',

        strength: {
            empty: 'No password',
            weak: 'Weak',
            fair: 'Fair',
            strong: 'Strong',
            veryStrong: 'Very strong',
        },
        requirements:
            'Use at least 8 characters with a mix of letters, numbers and symbols.',
    },

    auth: {
        email: 'Email address',
        password: 'Password',
        confirmPassword: 'Confirm password',

        login: {
            eyebrow: 'Welcome back',
            title: 'Welcome back',
            description:
                'Sign in to continue managing your household finances.',

            rememberMe: 'Remember me',
            forgotPassword: 'Forgot password?',

            submit: 'Sign in',
            submitting: 'Signing in...',

            noAccount: "Don't have an account?",
            createAccount: 'Create account',

            invalidCredentials:
                'The email address or password is incorrect.',

            emailNotVerified:
                'Your email address has not been verified yet.',

            success: 'Welcome back.',
        },

        register: {
            eyebrow: 'Get started',
            title: 'Create your Averio account',
            description:
                'Create your account and start building a clearer picture of your household finances.',

            passwordHint:
                "Choose a secure password that you don't use elsewhere.",

            submit: 'Create account',
            submitting: 'Creating account...',

            hasAccount: 'Already have an account?',
            signIn: 'Sign in',

            successTitle: 'Your account has been created',
            successDescription:
                'Check your inbox to verify your email address before signing in.',

            termsPrefix:
                "By creating an account, you agree to Averio's",
            terms: 'Terms of Service',
            privacy: 'Privacy Policy',
            termsJoin: 'and',
        },

        forgot: {
            eyebrow: 'Password recovery',
            title: 'Forgot your password?',
            description:
                "Enter your email address and we'll send you a link to reset your password.",

            submit: 'Send reset link',
            submitting: 'Sending...',

            backToLogin: 'Back to sign in',

            sentTitle: 'Check your inbox',
            sentDescription:
                "If an account exists for this email address, we've sent instructions for resetting the password.",
        },

        reset: {
            eyebrow: 'Password recovery',
            title: 'Create a new password',
            description:
                'Choose a new password for your Averio account.',

            newPassword: 'New password',
            confirmNewPassword: 'Confirm new password',

            submit: 'Reset password',
            submitting: 'Resetting password...',

            successTitle: 'Password changed',
            successDescription:
                'Your password has been reset successfully. You can now sign in with your new password.',

            backToLogin: 'Back to sign in',
            continueToLogin: 'Continue to sign in',

            invalidLinkTitle: 'Invalid reset link',
            invalidLinkDescription:
                'This password reset link is invalid or has expired. Request a new one to continue.',

            requestNewLink: 'Request a new reset link',
        },

        verify: {
            checkEmailTitle: 'Check your inbox',

            checkEmailDescription:
                "We've sent a verification email to {{email}}. Open the email and follow the link to verify your account.",

            verifying: 'Verifying your email...',

            successTitle: 'Email verified',
            successDescription:
                'Your email address has been verified successfully. You can now continue to Averio.',

            failureTitle: 'Verification failed',
            failureDescription:
                'This verification link is invalid or has expired.',

            continue: 'Continue to Averio',
            backToLogin: 'Back to sign in',
        },

        resend: {
            eyebrow: 'Email verification',
            title: 'Resend verification email',

            description:
                "Enter the email address you used to create your Averio account and we'll send you a new verification link.",

            submit: 'Resend verification email',
            submitting: 'Sending...',

            successTitle: 'Verification email sent',

            successDescription:
                "If an account exists for this email address and still needs verification, we've sent a new verification link. Please check your inbox.",

            backToLogin: 'Back to sign in',

            link: 'Resend verification email',
        },

        logout: {
            success: 'You have been signed out.',
        },

        session: {
            expiredTitle: 'Session expired',

            expiredDescription:
                'Your session has expired. Please sign in again to continue.',

            unauthorizedTitle: 'Sign in required',

            unauthorizedDescription:
                'Please sign in to continue.',
        },
    },

    onboarding: {
        eyebrow: 'Welcome to Averio',

        title: 'Set up your household',

        description:
            'A few details will help us create your financial home and personalize Averio for you.',

        householdOfOne:
            'Your household starts with you. You can invite someone later when you are ready to manage finances together.',

        displayName: 'Your name',

        householdName: 'Household name',

        country: 'Country',

        currency: 'Default currency',

        continue: 'Continue',
        common: {
            title: 'Make Averio yours',

            subtitle:
                'A few quick steps will help us prepare Averio for the way you manage money.',

            step: 'Step {{current}} of {{total}}',

            continue: 'Continue',
            saving: 'Saving...',
            saved: 'Saved',
            skip: 'Skip for now',
            finish: 'Finish setup',
        },

        welcome: {
            eyebrow: 'Welcome to Averio',

            title: "Let's set up your financial home",

            description:
                'Averio organizes your finances around a household. That household can start with just you and grow later if you decide to manage money with someone else.',

            start: 'Start setup',
        },

        profile: {
            title: 'Tell us a little about you',

            description:
                'This helps personalize Averio. You can change these details later.',

            fullNameLabel: 'Full name',
            fullNamePlaceholder: 'Your name',

            countryLabel: 'Country',
            countryPlaceholder: 'Select your country',

            genderLabel: 'Gender',
            genderPlaceholder: 'Select an option',

            genderMale: 'Male',
            genderFemale: 'Female',
            genderOther: 'Other',
            genderPreferNotToSay: 'Prefer not to say',
        },

        household: {
            eyebrow: 'Your household',
            title: 'Start with your household',

            description:
                'Every Averio account belongs to a household. For now, this can simply be your own personal household.',

            householdOfOneTitle: 'Start on your own',

            householdOfOneDescription:
                'Create a household with just you. You can invite or join someone later without rebuilding your finances.',

            createTitle: 'Create your household',

            nameLabel: 'Household name',
            namePlaceholder: 'e.g. My household',
            defaultName: 'My household',

            helper:
                'The name is only used to help identify your household inside Averio.',

            privacyTitle: 'You stay in control',

            privacyDescription:
                "Joining a household doesn't automatically mean every financial account or transaction has to become shared.",
        },

        firstAccount: {
            eyebrow: 'Your first account',

            title: 'Add the account you use most',

            description:
                'Accounts give Averio the starting point for understanding where your money lives.',

            nameLabel: 'Account name',
            namePlaceholder: 'e.g. Main checking account',

            typeLabel: 'Account type',
            typePlaceholder: 'Select account type',

            currencyLabel: 'Currency',
            currencyPlaceholder: 'Select currency',

            balanceLabel: 'Current balance',
            balancePlaceholder: '0.00',

            balanceHelper:
                'Use the balance currently available on this account.',

            skipDescription:
                'You can also skip this step and create an account later.',
        },

        complete: {
            title: "You're ready to use Averio",

            description:
                'Your household is set up. You can now start adding accounts, transactions and goals.',

            action: 'Continue to Averio',
        },

        resume: {
            title: 'Continue your setup',

            description:
                'We saved your progress, so you can continue where you left off.',

            action: 'Continue setup',
        },
    },

    navigation: {
        dashboard: 'Dashboard',
        household: 'Household',
        accounts: 'Accounts',
        transactions: 'Transactions',
        recurringTransactions: 'Recurring transactions',
        goals: 'Goals',
        statistics: 'Statistics',
        settings: 'Settings',
        profile: 'Profile',
    },

    dashboard: {
        title: 'Dashboard',
        greeting: 'Welcome back, {{name}}',

        householdOverview: 'Household overview',
        currentBalance: 'Current balance',

        monthlyIncome: 'Income this month',
        monthlyExpenses: 'Expenses this month',

        latestTransactions: 'Latest transactions',
        spendingByCategory: 'Spending by category',

        viewAllTransactions: 'View all transactions',

        empty: {
            title: 'Your financial picture starts here',

            description:
                'Add an account and your first transactions to start building your dashboard.',
        },
    },

    household: {
        title: 'Household',

        description:
            'Manage the people and financial structure that make up your household.',

        members: 'Members',
        member: 'Member',
        owner: 'Owner',
        you: 'You',

        householdName: 'Household name',
        createdAt: 'Created',

        inviteMember: 'Invite member',
        leaveHousehold: 'Leave household',

        personalHousehold: 'Personal household',
        sharedHousehold: 'Shared household',

        empty: {
            title: 'Your household starts with you',

            description:
                "You can keep using Averio on your own or invite someone when you're ready to manage finances together.",
        },
    },

    accounts: {
        title: 'Accounts',

        description:
            "See where your household's money is held.",

        addAccount: 'Add account',

        accountName: 'Account name',
        accountType: 'Account type',
        currency: 'Currency',
        balance: 'Balance',

        types: {
            checking: 'Checking account',
            savings: 'Savings account',
            cash: 'Cash',
            credit: 'Credit card',
        },

        empty: {
            title: 'No accounts yet',

            description:
                'Add your first account to start tracking balances and transactions.',

            action: 'Add account',
        },
    },

    transactions: {
        title: 'Transactions',

        addTransaction: 'Add transaction',

        income: 'Income',
        expense: 'Expense',
        transfer: 'Transfer',

        date: 'Date',
        account: 'Account',
        category: 'Category',
        amount: 'Amount',

        merchant: 'Merchant',
        description: 'Description',

        recurring: 'Recurring',
        latest: 'Latest transactions',

        empty: {
            title: 'No transactions yet',

            description:
                'Your transactions will appear here once you start adding them.',
        },
    },

    goals: {
        title: 'Goals',

        addGoal: 'Add goal',

        targetAmount: 'Target amount',
        currentAmount: 'Current amount',
        targetDate: 'Target date',

        empty: {
            title: 'No goals yet',

            description:
                "Create a financial goal and track your household's progress toward it.",
        },
    },

    statistics: {
        title: 'Statistics',

        income: 'Income',
        expenses: 'Expenses',
        balance: 'Balance',

        categories: 'Categories',
        period: 'Period',

        thisMonth: 'This month',
        lastMonth: 'Last month',
        thisYear: 'This year',
    },

    validation: {
        required: 'This field is required.',

        invalidEmail:
            'Enter a valid email address.',

        passwordTooShort:
            'Password must contain at least {{min}} characters.',

        passwordsDoNotMatch:
            'The passwords do not match.',

        invalidAmount:
            'Enter a valid amount.',

        amountMustBePositive:
            'The amount must be greater than zero.',

        nameTooLong:
            'The name must contain no more than {{max}} characters.',

        invalidSelection:
            'Select a valid option.',
    },

    errors: {
        genericTitle: 'Something went wrong',

        genericDescription:
            "We couldn't complete your request. Please try again.",

        networkTitle: 'Connection problem',

        networkDescription:
            "We couldn't reach Averio. Check your connection and try again.",

        unauthorized:
            "You don't have permission to perform this action.",

        forbidden:
            "You don't have access to this resource.",

        notFound:
            'The requested resource could not be found.',

        conflict:
            'This action conflicts with the current state of your data.',

        validation:
            'Some of the information you entered needs attention.',
    },

    notifications: {
        changesSaved:
            'Changes saved.',

        accountCreated:
            'Account created successfully.',

        accountUpdated:
            'Account updated successfully.',

        householdCreated:
            'Household created successfully.',

        profileUpdated:
            'Profile updated successfully.',

        transactionCreated:
            'Transaction added successfully.',

        goalCreated:
            'Goal created successfully.',
    },

    accessibility: {
        openMenu: 'Open menu',
        closeMenu: 'Close menu',

        showPassword: 'Show password',
        hidePassword: 'Hide password',

        openLanguageSelector: 'Change language',
        openThemeSelector: 'Change theme',

        logoAlt: 'Averio',
    },

    notFound: {
        code: '404',

        title: 'Page not found',

        description:
            "The page you're looking for doesn't exist or may have been moved.",

        action: 'Go to dashboard',
    },
} as const
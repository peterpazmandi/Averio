export const de = {
    app: {
        name: 'Averio',
        description: 'Haushaltsfinanzen klar verstehen.',
    },

    system: {
        title: 'Systemstatus',
        api: 'API',
        database: 'Datenbank',
        connected: 'Verbunden',
        unavailable: 'Nicht verfügbar',
        loading: 'Systemstatus wird geprüft…',
        error: 'Die Averio-API konnte nicht erreicht werden.',
        checkedAt: 'Geprüft {{date}}',
    },

    authShell: {
        eyebrow:
            'Haushaltsfinanzen, gemeinsam',
        title:
            'Finanzen werden einfacher, wenn der ganze Haushalt den Überblick hat.',
        description:
            'Plane gemeinsame Finanzen und behalte gleichzeitig die Flexibilität jedes Einzelnen.',
        footer:
            'Averio — Haushaltsfinanzen klar gemacht.',
    },

    preferences: {
        theme: {
            light: 'Heller Modus',
            dark: 'Dunkler Modus',
        },

        language: {
            label: 'Sprache',
            en: 'Englisch',
            de: 'Deutsch',
            hu: 'Ungarisch',
        },
    },

    auth: {
        email: 'E-Mail-Adresse',
        password: 'Passwort',
        confirmPassword:
            'Passwort bestätigen',

        register: {
            eyebrow:
                'Averio-Konto erstellen',
            title:
                'Starte mit deinem Haushalt',
            description:
                'Nutze Averio zunächst allein oder lade später jemanden ein.',
            passwordHint:
                'Verwende mindestens 12 Zeichen.',
            submit:
                'Konto erstellen',
            hasAccount:
                'Du hast bereits ein Konto?',
        },

        login: {
            eyebrow:
                'Willkommen zurück',
            title:
                'Bei Averio anmelden',
            description:
                'Mach dort weiter, wo dein Haushalt aufgehört hat.',
            rememberMe:
                'Angemeldet bleiben',
            forgotPassword:
                'Passwort vergessen?',
            submit:
                'Anmelden',
            noAccount:
                'Du hast noch kein Konto?',
        },

        forgot: {
            eyebrow:
                'Kontowiederherstellung',
            title:
                'Passwort vergessen?',
            description:
                'Gib deine E-Mail-Adresse ein. Wir senden dir einen Link, über den du ein neues Passwort festlegen kannst.',
            submit:
                'Link senden',
            sentTitle:
                'Prüfe deine E-Mails',
            sentDescription:
                'Falls für diese E-Mail-Adresse ein Averio-Konto existiert, haben wir einen Link zum Zurücksetzen des Passworts gesendet.',
            backToLogin:
                'Zurück zur Anmeldung',
        },

        reset: {
            eyebrow:
                'Kontowiederherstellung',
            title:
                'Neues Passwort festlegen',
            description:
                'Lege ein neues Passwort für dein Averio-Konto fest.',
            submit:
                'Neues Passwort speichern',
            successTitle:
                'Passwort geändert',
            successDescription:
                'Dein Passwort wurde aktualisiert. Du kannst dich jetzt anmelden.',
            invalidTitle:
                'Dieser Link funktioniert nicht mehr',
            invalidDescription:
                'Der Link zum Zurücksetzen des Passworts ist ungültig oder abgelaufen.',
        },

        verify: {
            checkEmailTitle:
                'Prüfe deine E-Mails',
            checkEmailDescription:
                'Wir haben dir einen Bestätigungslink gesendet. Öffne ihn, um dein Averio-Konto zu aktivieren.',
            verifying:
                'E-Mail-Adresse wird bestätigt…',
            successTitle:
                'E-Mail bestätigt',
            successDescription:
                'Dein Averio-Konto ist bereit. Du kannst dich jetzt anmelden.',
            failedTitle:
                'Dieser Link funktioniert nicht mehr',
            failedDescription:
                'Der Bestätigungslink ist ungültig oder abgelaufen.',
        },

        resend: {
            link:
                'Bestätigungs-E-Mail erneut senden',
            title:
                'Bestätigungs-E-Mail erneut senden',
            description:
                'Gib deine E-Mail-Adresse ein und wir senden dir einen neuen Bestätigungslink.',
            submit:
                'Bestätigungs-E-Mail senden',
            sent:
                'Falls für diese E-Mail-Adresse ein unbestätigtes Averio-Konto existiert, wurde ein neuer Link gesendet.',
        },
    },

    password: {
        show: 'Passwort anzeigen',
        hide: 'Passwort ausblenden',

        mismatch:
            'Die Passwörter stimmen nicht überein.',

        strength: {
            empty: '',
            weak: 'Schwaches Passwort',
            fair: 'Akzeptables Passwort',
            strong: 'Starkes Passwort',
            veryStrong:
                'Sehr starkes Passwort',
        },
    },

    language: {
        label: 'Sprache',
        en: 'English',
        de: 'Deutsch',
        hu: 'Magyar',
    },
} as const;
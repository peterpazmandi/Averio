export const de = {
    common: {
        appName: 'Averio',
        requestFailed: 'Anfrage fehlgeschlagen',
        reference: 'Referenz',
        unexpectedError: 'Etwas ist schiefgelaufen',
        actions: {
            continue: 'Weiter',
            back: 'Zurück',
            next: 'Weiter',
            save: 'Speichern',
            saving: 'Wird gespeichert...',
            cancel: 'Abbrechen',
            close: 'Schließen',
            done: 'Fertig',
            edit: 'Bearbeiten',
            delete: 'Löschen',
            remove: 'Entfernen',
            retry: 'Erneut versuchen',
            confirm: 'Bestätigen',
            skip: 'Vorerst überspringen',
            signOut: 'Abmelden',
            signingOut: 'Abmeldung...',
        },

        status: {
            loading: 'Wird geladen...',
            saving: 'Wird gespeichert...',
            success: 'Erfolgreich',
            error: 'Etwas ist schiefgelaufen',
        },

        yes: 'Ja',
        no: 'Nein',
        optional: 'Optional',
        required: 'Erforderlich',

        language: {
            label: 'Sprache',
            english: 'Englisch',
            german: 'Deutsch',
            hungarian: 'Ungarisch',
        },

        theme: {
            label: 'Darstellung',
            light: 'Hell',
            dark: 'Dunkel',
            system: 'System',
            switchToLight: 'Zum hellen Modus wechseln',
            switchToDark: 'Zum dunklen Modus wechseln',
        },
    },
    preferences: {
        language: {
            label: 'Sprache',
        },

        theme: {
            light: 'Hell',
            dark: 'Dunkel',
        },
    },

    authShell: {
        eyebrow: 'Haushaltsfinanzen einfach im Blick',

        title:
            'Ein Ort für die Finanzen, die ihr gemeinsam verwaltet.',

        description:
            'Averio hilft dir, Konten, Transaktionen, Ziele und Haushaltsfinanzen übersichtlich zu organisieren, ohne deine persönlichen Finanzen aus den Augen zu verlieren.',

        footer:
            'Deine Finanzen. Dein Haushalt. Deine Entscheidung.',
    },

    password: {
        mismatch:
            'Die Passwörter stimmen nicht überein.',

        strengthLabel:
            'Passwortstärke',

        strength: {
            empty: 'Kein Passwort',
            weak: 'Schwach',
            fair: 'Ausreichend',
            strong: 'Stark',
            veryStrong: 'Sehr stark',
        },
        requirements: 'Verwende mindestens 8 Zeichen mit einer Mischung aus Buchstaben, Zahlen und Symbolen.',
        show: 'Passwort anzeigen',
        hide: 'Passwort verbergen',

        validation: {
            minimumLength: 'Das Passwort muss mindestens {{min}} Zeichen enthalten.',
            confirmRequired: 'Bestätige dein Passwort.',
            rejected: 'Wähle ein stärkeres Passwort.',
        },
    },

    auth: {
        email: 'E-Mail-Adresse',
        password: 'Passwort',
        confirmPassword: 'Passwort bestätigen',

        login: {
            eyebrow: 'Willkommen zurück',
            title: 'Willkommen zurück',

            description:
                'Melde dich an, um deine Haushaltsfinanzen weiter zu verwalten.',

            rememberMe: 'Angemeldet bleiben',
            forgotPassword: 'Passwort vergessen?',

            submit: 'Anmelden',
            submitting: 'Anmeldung läuft...',

            noAccount: 'Noch kein Konto?',
            createAccount: 'Konto erstellen',

            invalidCredentials:
                'Die E-Mail-Adresse oder das Passwort ist nicht korrekt.',

            emailNotVerified:
                'Deine E-Mail-Adresse wurde noch nicht bestätigt.',

            success:
                'Willkommen zurück.',
        },

        register: {
            eyebrow: 'Jetzt starten',

            title:
                'Erstelle dein Averio-Konto',

            description:
                'Erstelle dein Konto und verschaffe dir einen klareren Überblick über die Finanzen deines Haushalts.',

            passwordHint:
                'Wähle ein sicheres Passwort, das du nicht für andere Dienste verwendest.',

            submit:
                'Konto erstellen',

            submitting:
                'Konto wird erstellt...',

            hasAccount:
                'Du hast bereits ein Konto?',

            signIn:
                'Anmelden',

            successTitle:
                'Dein Konto wurde erstellt',

            successDescription:
                'Überprüfe deinen Posteingang und bestätige deine E-Mail-Adresse, bevor du dich anmeldest.',

            termsPrefix:
                'Mit der Erstellung eines Kontos akzeptierst du Averios',

            terms:
                'Nutzungsbedingungen',

            privacy:
                'Datenschutzerklärung',

            termsJoin:
                'und',
        },

        forgot: {
            eyebrow:
                'Passwort wiederherstellen',

            title:
                'Passwort vergessen?',

            description:
                'Gib deine E-Mail-Adresse ein und wir senden dir einen Link zum Zurücksetzen deines Passworts.',

            submit:
                'Link zum Zurücksetzen senden',

            submitting:
                'Wird gesendet...',

            backToLogin:
                'Zurück zur Anmeldung',

            sentTitle:
                'Überprüfe deinen Posteingang',

            sentDescription:
                'Falls ein Konto mit dieser E-Mail-Adresse existiert, haben wir dir Anweisungen zum Zurücksetzen des Passworts gesendet.',
        },

        reset: {
            eyebrow:
                'Passwort wiederherstellen',

            title:
                'Neues Passwort erstellen',

            description:
                'Wähle ein neues Passwort für dein Averio-Konto.',

            newPassword:
                'Neues Passwort',

            confirmNewPassword:
                'Neues Passwort bestätigen',

            submit:
                'Passwort zurücksetzen',

            submitting:
                'Passwort wird zurückgesetzt...',

            successTitle:
                'Passwort geändert',

            successDescription:
                'Dein Passwort wurde erfolgreich zurückgesetzt. Du kannst dich jetzt mit deinem neuen Passwort anmelden.',

            backToLogin:
                'Zurück zur Anmeldung',

            continueToLogin:
                'Weiter zur Anmeldung',

            invalidLinkTitle:
                'Ungültiger Link',

            invalidLinkDescription:
                'Dieser Link zum Zurücksetzen des Passworts ist ungültig oder abgelaufen. Fordere einen neuen Link an, um fortzufahren.',

            requestNewLink:
                'Neuen Link anfordern',
            invalidTitle: 'Ungültiger Link',
            invalidDescription:
                'Dieser Link zum Zurücksetzen des Passworts ist ungültig oder abgelaufen. Fordere einen neuen Link an, um fortzufahren.',
            requestAnother: 'Neuen Link anfordern',
        },

        verify: {
            checkEmailTitle:
                'Überprüfe deinen Posteingang',

            checkEmailDescription:
                'Wir haben eine Bestätigungs-E-Mail an {{email}} gesendet. Öffne die E-Mail und folge dem Link, um dein Konto zu bestätigen.',

            verifying:
                'E-Mail-Adresse wird bestätigt...',

            successTitle:
                'E-Mail-Adresse bestätigt',

            successDescription:
                'Deine E-Mail-Adresse wurde erfolgreich bestätigt. Du kannst jetzt mit Averio fortfahren.',

            failureTitle:
                'Bestätigung fehlgeschlagen',

            failureDescription:
                'Dieser Bestätigungslink ist ungültig oder abgelaufen.',

            continue:
                'Weiter zu Averio',

            backToLogin:
                'Zurück zur Anmeldung',
            failedTitle: 'Bestätigung fehlgeschlagen',
            failedDescription:
                'Dieser Bestätigungslink ist ungültig oder abgelaufen.',
        },

        resend: {
            eyebrow:
                'E-Mail-Bestätigung',

            title:
                'Bestätigungs-E-Mail erneut senden',

            description:
                'Gib die E-Mail-Adresse ein, mit der du dein Averio-Konto erstellt hast. Wir senden dir einen neuen Bestätigungslink.',

            submit:
                'Bestätigungs-E-Mail erneut senden',

            submitting:
                'Wird gesendet...',

            successTitle:
                'Bestätigungs-E-Mail gesendet',

            successDescription:
                'Falls ein Konto mit dieser E-Mail-Adresse existiert und noch bestätigt werden muss, haben wir einen neuen Bestätigungslink gesendet. Bitte überprüfe deinen Posteingang.',

            backToLogin:
                'Zurück zur Anmeldung',

            link:
                'Bestätigungs-E-Mail erneut senden',
        },

        logout: {
            success:
                'Du wurdest abgemeldet.',
        },

        session: {
            expiredTitle:
                'Sitzung abgelaufen',

            expiredDescription:
                'Deine Sitzung ist abgelaufen. Bitte melde dich erneut an, um fortzufahren.',

            unauthorizedTitle:
                'Anmeldung erforderlich',

            unauthorizedDescription:
                'Bitte melde dich an, um fortzufahren.',
        },
    },

    onboarding: {
        eyebrow: 'Willkommen bei Averio',

        title: 'Richte deinen Haushalt ein',

        description:
            'Mit ein paar Angaben können wir dein finanzielles Zuhause einrichten und Averio auf dich abstimmen.',

        householdOfOne:
            'Dein Haushalt beginnt mit dir. Du kannst später jemanden einladen, wenn ihr eure Finanzen gemeinsam verwalten möchtet.',

        displayName: 'Dein Name',

        householdName: 'Name des Haushalts',

        country: 'Land',

        currency: 'Standardwährung',

        continue: 'Weiter',
        common: {
            title:
                'Mach Averio zu deinem Averio',

            subtitle:
                'Mit wenigen Schritten können wir Averio an die Art anpassen, wie du deine Finanzen verwaltest.',

            step:
                'Schritt {{current}} von {{total}}',

            continue:
                'Weiter',

            saving:
                'Wird gespeichert...',

            saved:
                'Gespeichert',

            skip:
                'Vorerst überspringen',

            finish:
                'Einrichtung abschließen',
        },

        welcome: {
            eyebrow:
                'Willkommen bei Averio',

            title:
                'Richten wir dein finanzielles Zuhause ein',

            description:
                'Averio organisiert deine Finanzen rund um einen Haushalt. Dieser Haushalt kann zunächst nur aus dir bestehen und später wachsen, wenn du deine Finanzen mit jemandem gemeinsam verwalten möchtest.',

            start:
                'Einrichtung starten',
        },

        profile: {
            title:
                'Erzähl uns ein wenig über dich',

            description:
                'Damit können wir Averio besser auf dich abstimmen. Du kannst diese Angaben später jederzeit ändern.',

            fullNameLabel:
                'Vollständiger Name',

            fullNamePlaceholder:
                'Dein Name',

            countryLabel:
                'Land',

            countryPlaceholder:
                'Land auswählen',

            genderLabel:
                'Geschlecht',

            genderPlaceholder:
                'Option auswählen',

            genderMale:
                'Männlich',

            genderFemale:
                'Weiblich',

            genderOther:
                'Andere',

            genderPreferNotToSay:
                'Keine Angabe',
        },

        household: {
            eyebrow:
                'Dein Haushalt',

            title:
                'Beginne mit deinem Haushalt',

            description:
                'Jedes Averio-Konto gehört zu einem Haushalt. Zu Beginn kann das einfach dein persönlicher Haushalt sein.',

            householdOfOneTitle:
                'Alleine starten',

            householdOfOneDescription:
                'Erstelle einen Haushalt nur für dich. Später kannst du jemanden einladen oder einem anderen Haushalt beitreten, ohne deine Finanzen neu aufbauen zu müssen.',

            createTitle:
                'Erstelle deinen Haushalt',

            nameLabel:
                'Name des Haushalts',

            namePlaceholder:
                'z. B. Mein Haushalt',

            defaultName:
                'Mein Haushalt',

            helper:
                'Der Name dient lediglich dazu, deinen Haushalt innerhalb von Averio leichter zu erkennen.',

            privacyTitle:
                'Du behältst die Kontrolle',

            privacyDescription:
                'Ein gemeinsamer Haushalt bedeutet nicht automatisch, dass jedes Konto oder jede Transaktion geteilt werden muss.',
        },

        firstAccount: {
            eyebrow:
                'Dein erstes Konto',

            title:
                'Füge das Konto hinzu, das du am häufigsten verwendest',

            description:
                'Konten sind der Ausgangspunkt für Averio, um zu verstehen, wo sich dein Geld befindet.',

            nameLabel:
                'Kontoname',

            namePlaceholder:
                'z. B. Hauptkonto',

            typeLabel:
                'Kontotyp',

            typePlaceholder:
                'Kontotyp auswählen',

            currencyLabel:
                'Währung',

            currencyPlaceholder:
                'Währung auswählen',

            balanceLabel:
                'Aktueller Kontostand',

            balancePlaceholder:
                '0,00',

            balanceHelper:
                'Gib den aktuell verfügbaren Kontostand dieses Kontos ein.',

            skipDescription:
                'Du kannst diesen Schritt auch überspringen und später ein Konto hinzufügen.',
        },

        complete: {
            title:
                'Averio ist bereit',

            description:
                'Dein Haushalt wurde eingerichtet. Jetzt kannst du Konten, Transaktionen und Ziele hinzufügen.',

            action:
                'Weiter zu Averio',
        },

        resume: {
            title:
                'Einrichtung fortsetzen',

            description:
                'Wir haben deinen Fortschritt gespeichert. Du kannst dort weitermachen, wo du aufgehört hast.',

            action:
                'Einrichtung fortsetzen',
        },
    },

    navigation: {
        dashboard: 'Übersicht',
        household: 'Haushalt',
        accounts: 'Konten',
        transactions: 'Transaktionen',
        recurringTransactions: 'Wiederkehrende Transaktionen',
        goals: 'Ziele',
        statistics: 'Statistiken',
        settings: 'Einstellungen',
        profile: 'Profil',
    },

    dashboard: {
        title:
            'Übersicht',

        greeting:
            'Willkommen zurück, {{name}}',

        householdOverview:
            'Haushaltsübersicht',

        currentBalance:
            'Aktueller Kontostand',

        monthlyIncome:
            'Einnahmen in diesem Monat',

        monthlyExpenses:
            'Ausgaben in diesem Monat',

        latestTransactions:
            'Letzte Transaktionen',

        spendingByCategory:
            'Ausgaben nach Kategorie',

        viewAllTransactions:
            'Alle Transaktionen anzeigen',

        empty: {
            title:
                'Hier beginnt deine finanzielle Übersicht',

            description:
                'Füge ein Konto und deine ersten Transaktionen hinzu, um deine Übersicht aufzubauen.',
        },
    },

    household: {
        title:
            'Haushalt',

        description:
            'Verwalte die Personen und die finanzielle Struktur deines Haushalts.',

        members:
            'Mitglieder',

        member:
            'Mitglied',

        owner:
            'Eigentümer',

        you:
            'Du',

        householdName:
            'Name des Haushalts',

        createdAt:
            'Erstellt',

        inviteMember:
            'Mitglied einladen',

        leaveHousehold:
            'Haushalt verlassen',

        personalHousehold:
            'Persönlicher Haushalt',

        sharedHousehold:
            'Gemeinsamer Haushalt',

        empty: {
            title:
                'Dein Haushalt beginnt mit dir',

            description:
                'Du kannst Averio alleine nutzen oder jemanden einladen, sobald ihr eure Finanzen gemeinsam verwalten möchtet.',
        },
    },

    accounts: {
        title:
            'Konten',

        description:
            'Sieh, wo sich das Geld deines Haushalts befindet.',

        addAccount:
            'Konto hinzufügen',

        accountName:
            'Kontoname',

        accountType:
            'Kontotyp',

        currency:
            'Währung',

        balance:
            'Kontostand',

        types: {
            checking:
                'Girokonto',

            savings:
                'Sparkonto',

            cash:
                'Bargeld',

            credit:
                'Kreditkarte',
        },

        empty: {
            title:
                'Noch keine Konten',

            description:
                'Füge dein erstes Konto hinzu, um Kontostände und Transaktionen zu erfassen.',

            action:
                'Konto hinzufügen',
        },
    },

    transactions: {
        title:
            'Transaktionen',

        addTransaction:
            'Transaktion hinzufügen',

        income:
            'Einnahme',

        expense:
            'Ausgabe',

        transfer:
            'Überweisung',

        date:
            'Datum',

        account:
            'Konto',

        category:
            'Kategorie',

        amount:
            'Betrag',

        merchant:
            'Händler',

        description:
            'Beschreibung',

        recurring:
            'Wiederkehrend',

        latest:
            'Letzte Transaktionen',

        empty: {
            title:
                'Noch keine Transaktionen',

            description:
                'Sobald du Transaktionen hinzufügst, werden sie hier angezeigt.',
        },
    },

    goals: {
        title:
            'Ziele',

        addGoal:
            'Ziel hinzufügen',

        targetAmount:
            'Zielbetrag',

        currentAmount:
            'Aktueller Betrag',

        targetDate:
            'Zieldatum',

        empty: {
            title:
                'Noch keine Ziele',

            description:
                'Erstelle ein finanzielles Ziel und verfolge den Fortschritt deines Haushalts.',
        },
    },

    statistics: {
        title:
            'Statistiken',

        income:
            'Einnahmen',

        expenses:
            'Ausgaben',

        balance:
            'Saldo',

        categories:
            'Kategorien',

        period:
            'Zeitraum',

        thisMonth:
            'Dieser Monat',

        lastMonth:
            'Letzter Monat',

        thisYear:
            'Dieses Jahr',
    },

    validation: {
        required:
            'Dieses Feld ist erforderlich.',

        invalidEmail:
            'Gib eine gültige E-Mail-Adresse ein.',

        passwordTooShort:
            'Das Passwort muss mindestens {{min}} Zeichen enthalten.',

        passwordsDoNotMatch:
            'Die Passwörter stimmen nicht überein.',

        invalidAmount:
            'Gib einen gültigen Betrag ein.',

        amountMustBePositive:
            'Der Betrag muss größer als null sein.',

        nameTooLong:
            'Der Name darf höchstens {{max}} Zeichen enthalten.',

        invalidSelection:
            'Wähle eine gültige Option aus.',
    },

    errors: {
        genericTitle:
            'Etwas ist schiefgelaufen',

        genericDescription:
            'Wir konnten deine Anfrage nicht abschließen. Bitte versuche es erneut.',

        networkTitle:
            'Verbindungsproblem',

        networkDescription:
            'Averio konnte nicht erreicht werden. Überprüfe deine Verbindung und versuche es erneut.',

        unauthorized:
            'Du bist nicht berechtigt, diese Aktion auszuführen.',

        forbidden:
            'Du hast keinen Zugriff auf diese Ressource.',

        notFound:
            'Die angeforderte Ressource wurde nicht gefunden.',

        conflict:
            'Diese Aktion steht im Konflikt mit dem aktuellen Zustand deiner Daten.',

        validation:
            'Einige deiner Eingaben müssen überprüft werden.',
    },

    notifications: {
        changesSaved:
            'Änderungen gespeichert.',

        accountCreated:
            'Konto erfolgreich erstellt.',

        accountUpdated:
            'Konto erfolgreich aktualisiert.',

        householdCreated:
            'Haushalt erfolgreich erstellt.',

        profileUpdated:
            'Profil erfolgreich aktualisiert.',

        transactionCreated:
            'Transaktion erfolgreich hinzugefügt.',

        goalCreated:
            'Ziel erfolgreich erstellt.',
    },

    accessibility: {
        openMenu:
            'Menü öffnen',

        closeMenu:
            'Menü schließen',

        showPassword:
            'Passwort anzeigen',

        hidePassword:
            'Passwort ausblenden',

        openLanguageSelector:
            'Sprache ändern',

        openThemeSelector:
            'Darstellung ändern',

        logoAlt:
            'Averio',
    },

    notFound: {
        code:
            '404',

        title:
            'Seite nicht gefunden',

        description:
            'Die gesuchte Seite existiert nicht oder wurde möglicherweise verschoben.',

        action:
            'Zur Übersicht',
    },
} as const
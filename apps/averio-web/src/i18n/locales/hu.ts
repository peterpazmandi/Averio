export const hu = {
    app: {
        name: 'Averio',
        description: 'Háztartási pénzügyek átláthatóan.',
    },

    system: {
        title: 'Rendszerállapot',
        api: 'API',
        database: 'Adatbázis',
        connected: 'Csatlakoztatva',
        unavailable: 'Nem elérhető',
        loading: 'Rendszerállapot ellenőrzése…',
        error: 'Az Averio API nem érhető el.',
        checkedAt: 'Ellenőrizve: {{date}}',
    },

    authShell: {
        eyebrow:
            'Háztartási pénzügyek, közösen',
        title:
            'A pénzügyek átláthatóbbak, ha az egész háztartás átlátható.',
        description:
            'Tervezd a közös pénzügyeket úgy, hogy közben mindenki megőrizhesse a számára szükséges rugalmasságot.',
        footer:
            'Averio — átlátható háztartási pénzügyek.',
    },

    preferences: {
        theme: {
            light: 'Világos mód',
            dark: 'Sötét mód',
        },

        language: {
            label: 'Nyelv',
            en: 'Angol',
            de: 'Német',
            hu: 'Magyar',
        },
    },

    auth: {
        email: 'E-mail-cím',
        password: 'Jelszó',
        confirmPassword:
            'Jelszó megerősítése',

        register: {
            eyebrow:
                'Averio-fiók létrehozása',
            title:
                'Kezdd a háztartásoddal',
            description:
                'Használd az Averiót egyedül, vagy később hívj meg valakit a háztartásodba.',
            passwordHint:
                'Használj legalább 12 karaktert.',
            submit:
                'Fiók létrehozása',
            hasAccount:
                'Már van fiókod?',
        },

        login: {
            eyebrow:
                'Üdv újra',
            title:
                'Bejelentkezés az Averióba',
            description:
                'Folytasd ott, ahol a háztartásod abbahagyta.',
            rememberMe:
                'Maradjak bejelentkezve',
            forgotPassword:
                'Elfelejtetted a jelszavad?',
            submit:
                'Bejelentkezés',
            noAccount:
                'Még nincs fiókod?',
        },

        forgot: {
            eyebrow:
                'Fiók helyreállítása',
            title:
                'Elfelejtetted a jelszavad?',
            description:
                'Add meg az e-mail-címedet, és küldünk egy hivatkozást, amellyel új jelszót állíthatsz be.',
            submit:
                'Visszaállítási link küldése',
            sentTitle:
                'Ellenőrizd az e-mailjeidet',
            sentDescription:
                'Ha ehhez az e-mail-címhez tartozik Averio-fiók, elküldtük a jelszó-visszaállítási hivatkozást.',
            backToLogin:
                'Vissza a bejelentkezéshez',
        },

        reset: {
            eyebrow:
                'Fiók helyreállítása',

            title:
                'Állíts be új jelszót',

            description:
                'Adj meg egy új jelszót az Averio-fiókodhoz.',

            submit:
                'Új jelszó mentése',

            successTitle:
                'A jelszó megváltozott',

            successDescription:
                'A jelszavad frissült. Most már bejelentkezhetsz.',

            invalidTitle:
                'Ez a hivatkozás már nem használható',

            invalidDescription:
                'A jelszó-visszaállítási hivatkozás érvénytelen vagy lejárt.',

            requestAnother:
                'Új visszaállítási link kérése',
        },

        verify: {
            checkEmailTitle:
                'Ellenőrizd az e-mailjeidet',
            checkEmailDescription:
                'Küldtünk egy megerősítő hivatkozást. Nyisd meg az Averio-fiókod aktiválásához.',
            verifying:
                'E-mail-cím megerősítése…',
            successTitle:
                'E-mail-cím megerősítve',
            successDescription:
                'Az Averio-fiókod készen áll. Most már bejelentkezhetsz.',
            failedTitle:
                'Ez a hivatkozás már nem használható',
            failedDescription:
                'A megerősítő hivatkozás érvénytelen vagy lejárt.',
        },

        resend: {
            link:
                'Új megerősítő e-mail küldése',
            title:
                'Megerősítő e-mail újraküldése',
            description:
                'Add meg az e-mail-címedet, és új megerősítő hivatkozást küldünk.',
            submit:
                'Megerősítő e-mail küldése',
            sent:
                'Ha ehhez az e-mail-címhez tartozik még nem megerősített Averio-fiók, új hivatkozást küldtünk.',
        },
    },

    password: {
        show:
            'Jelszó megjelenítése',

        hide:
            'Jelszó elrejtése',

        mismatch:
            'A két jelszó nem egyezik.',

        requirements:
            'Használj legalább 12 karaktert. A hosszabb és kevésbé kiszámítható jelszó biztonságosabb.',

        validation: {
            minimumLength:
                'A jelszónak legalább 12 karakterből kell állnia.',

            confirmRequired:
                'Erősítsd meg a jelszavad.',

            rejected:
                'Ez a jelszó nem felel meg a szükséges biztonsági követelményeknek.',
        },
        
        strengthLabel:
            'Jelszó erőssége',
        strength: {
            empty:
                '',

            weak:
                'Gyenge jelszó',

            fair:
                'Közepes jelszó',

            strong:
                'Erős jelszó',

            veryStrong:
                'Nagyon erős jelszó',
        },
    },

    common: {
        unexpectedError:
            'Valami hiba történt. Próbáld újra.',

        requestFailed:
            'A kérést nem sikerült végrehajtani.',

        reference:
            'Hivatkozási azonosító: {{reference}}',
    },

    language: {
        label: 'Nyelv',
        en: 'English',
        de: 'Deutsch',
        hu: 'Magyar',
    },
} as const;
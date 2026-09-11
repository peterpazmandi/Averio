export const hu = {
    common: {
        appName: 'Averio',
        requestFailed: 'A kérés sikertelen',
        reference: 'Hivatkozás: {{reference}}',
        unexpectedError: 'Valami hiba történt',
        actions: {
            continue: 'Folytatás',
            back: 'Vissza',
            next: 'Tovább',
            save: 'Mentés',
            saving: 'Mentés...',
            cancel: 'Mégse',
            close: 'Bezárás',
            done: 'Kész',
            edit: 'Szerkesztés',
            delete: 'Törlés',
            remove: 'Eltávolítás',
            retry: 'Próbáld újra',
            confirm: 'Megerősítés',
            skip: 'Kihagyás egyelőre',
            signOut: 'Kijelentkezés',
            signingOut: 'Kijelentkezés...',
        },

        status: {
            loading: 'Betöltés...',
            saving: 'Mentés...',
            success: 'Sikeres',
            error: 'Valami hiba történt',
        },

        yes: 'Igen',
        no: 'Nem',
        optional: 'Opcionális',
        required: 'Kötelező',

        language: {
            label: 'Nyelv',
            english: 'Angol',
            german: 'Német',
            hungarian: 'Magyar',
        },

        theme: {
            label: 'Megjelenés',
            light: 'Világos',
            dark: 'Sötét',
            system: 'Rendszer',
            switchToLight: 'Váltás világos módra',
            switchToDark: 'Váltás sötét módra',
        },
    },
    preferences: {
        language: {
            label: 'Nyelv',
        },

        theme: {
            light: 'Világos',
            dark: 'Sötét',
        },
    },

    authShell: {
        eyebrow:
            'Átláthatóbb háztartási pénzügyek',

        title:
            'Egy hely a pénzügyekhez, amelyeket együtt kezeltek.',

        description:
            'Az Averio segít átláthatóan kezelni a számlákat, tranzakciókat, célokat és a háztartás pénzügyeit úgy, hogy közben a személyes pénzügyeid is elkülöníthetők maradnak.',

        footer:
            'A te pénzügyeid. A te háztartásod. A te döntésed.',
    },

    password: {
        mismatch:
            'A két jelszó nem egyezik.',

        strengthLabel: 'Jelszó erőssége',

        strength: {
            empty:
                'Nincs jelszó',

            weak:
                'Gyenge',

            fair:
                'Elfogadható',

            strong:
                'Erős',

            veryStrong:
                'Nagyon erős',
        },
        requirements: 'Használj legalább {{min}} karaktert, betűk, számok és szimbólumok keverékével.',
        show: 'Jelszó megjelenítése',
        hide: 'Jelszó elrejtése',

        validation: {
            minimumLength: 'A jelszónak legalább {{min}} karakter hosszúnak kell lennie.',
            confirmRequired: 'Erősítsd meg a jelszavad.',
            rejected: 'Válassz erősebb jelszót.',
        },
    },

    auth: {
        email:
            'E-mail-cím',

        password:
            'Jelszó',

        confirmPassword:
            'Jelszó megerősítése',

        login: {
            eyebrow:
                'Üdv újra',

            title:
                'Üdv újra',

            description:
                'Jelentkezz be, hogy tovább kezeld a háztartásod pénzügyeit.',

            rememberMe:
                'Maradjak bejelentkezve',

            forgotPassword:
                'Elfelejtetted a jelszavad?',

            submit:
                'Bejelentkezés',

            submitting:
                'Bejelentkezés...',

            noAccount:
                'Még nincs fiókod?',

            createAccount:
                'Fiók létrehozása',

            invalidCredentials:
                'Az e-mail-cím vagy a jelszó nem megfelelő.',

            emailNotVerified:
                'Az e-mail-címed még nincs megerősítve.',

            success:
                'Üdv újra.',
        },

        register: {
            eyebrow:
                'Kezdjük el',

            title:
                'Hozd létre az Averio-fiókodat',

            description:
                'Hozd létre a fiókodat, és kezdd el átláthatóbban kezelni a háztartásod pénzügyeit.',

            passwordHint:
                'Válassz biztonságos jelszót, amelyet máshol nem használsz.',

            submit:
                'Fiók létrehozása',

            submitting:
                'Fiók létrehozása...',

            hasAccount:
                'Már van fiókod?',

            signIn:
                'Bejelentkezés',

            successTitle:
                'A fiókod elkészült',

            sentTitle:
                'Ellenőrizd a postaládádat',

            sentDescription:
                'Ha létezik fiók ezzel az e-mail-címmel, küldtünk egy linket a jelszó visszaállításához.',

            termsPrefix:
                'A fiók létrehozásával elfogadod az Averio',

            terms:
                'Felhasználási feltételeit',

            privacy:
                'Adatvédelmi szabályzatát',

            termsJoin:
                'és',
        },

        forgot: {
            eyebrow:
                'Jelszó-visszaállítás',

            title:
                'Elfelejtetted a jelszavad?',

            description:
                'Add meg az e-mail-címedet, és küldünk egy linket, amellyel új jelszót állíthatsz be.',

            submit:
                'Visszaállító link küldése',

            submitting:
                'Küldés...',

            backToLogin:
                'Vissza a bejelentkezéshez',

            successTitle:
                'Ellenőrizd a postaládádat',

            successDescription:
                'Ha létezik fiók ezzel az e-mail-címmel, elküldtük a jelszó visszaállításához szükséges lépéseket.',
        },

        reset: {
            eyebrow:
                'Jelszó-visszaállítás',

            title:
                'Új jelszó létrehozása',

            description:
                'Adj meg egy új jelszót az Averio-fiókodhoz.',

            newPassword:
                'Új jelszó',

            confirmNewPassword:
                'Új jelszó megerősítése',

            submit:
                'Jelszó visszaállítása',

            submitting:
                'Jelszó visszaállítása...',

            successTitle:
                'Jelszó módosítva',

            successDescription:
                'A jelszavadat sikeresen visszaállítottuk. Most már bejelentkezhetsz az új jelszavaddal.',

            backToLogin:
                'Vissza a bejelentkezéshez',

            continueToLogin:
                'Tovább a bejelentkezéshez',

            invalidLinkTitle:
                'Érvénytelen link',

            invalidLinkDescription:
                'Ez a jelszó-visszaállító link érvénytelen vagy lejárt. A folytatáshoz kérj egy új linket.',

            requestNewLink:
                'Új link kérése',

            invalidTitle: 'Érvénytelen link',
            invalidDescription:
                'Ez a jelszó-visszaállító link érvénytelen vagy lejárt. A folytatáshoz kérj egy új linket.',
            requestAnother: 'Új link kérése',
        },

        verify: {
            checkEmailTitle:
                'Ellenőrizd a postaládádat',

            checkEmailDescription:
                'Megerősítő e-mailt küldtünk erre a címre: {{email}}. Nyisd meg az e-mailt, és kövesd a linket a fiókod megerősítéséhez.',

            verifying:
                'E-mail-cím ellenőrzése...',

            successTitle:
                'E-mail-cím megerősítve',

            successDescription:
                'Az e-mail-címedet sikeresen megerősítettük. Most már folytathatod az Averio használatát.',

            failureTitle:
                'A megerősítés sikertelen',

            failureDescription:
                'Ez a megerősítő link érvénytelen vagy lejárt.',

            continue:
                'Tovább az Averióhoz',

            backToLogin:
                'Vissza a bejelentkezéshez',
            failedTitle: 'A megerősítés sikertelen',
            failedDescription:
                'Ez a megerősítő link érvénytelen vagy lejárt.',
            checkEmailDescriptionWithoutEmail:
                'Elküldtük az ellenőrző e-mailt. Nyisd meg az e-mailt, és kövesd a linket a fiókod megerősítéséhez.',
        },

        resend: {
            eyebrow:
                'E-mail-cím megerősítése',

            title:
                'Megerősítő e-mail újraküldése',

            description:
                'Add meg azt az e-mail-címet, amellyel létrehoztad az Averio-fiókodat, és küldünk egy új megerősítő linket.',

            submit:
                'Megerősítő e-mail újraküldése',

            submitting:
                'Küldés...',

            successTitle:
                'Megerősítő e-mail elküldve',

            successDescription:
                'Ha létezik fiók ezzel az e-mail-címmel, és még megerősítésre vár, elküldtünk egy új megerősítő linket. Ellenőrizd a postaládádat.',

            backToLogin:
                'Vissza a bejelentkezéshez',

            link:
                'Megerősítő e-mail újraküldése',
        },

        logout: {
            success:
                'Sikeresen kijelentkeztél.',
        },

        session: {
            expiredTitle:
                'A munkamenet lejárt',

            expiredDescription:
                'A munkameneted lejárt. A folytatáshoz jelentkezz be újra.',

            unauthorizedTitle:
                'Bejelentkezés szükséges',

            unauthorizedDescription:
                'A folytatáshoz jelentkezz be.',
        },
    },

    onboarding: {

        eyebrow: 'Üdv az Averióban',

        title: 'Állítsd be a háztartásodat',

        description:
            'Néhány adat segítségével létrehozzuk a pénzügyi otthonodat, és személyre szabjuk számodra az Averiót.',

        householdOfOne:
            'A háztartásod veled kezdődik. Később meghívhatsz valakit, amikor közösen szeretnétek kezelni a pénzügyeiteket.',

        displayName: 'A neved',

        householdName: 'Háztartás neve',

        country: 'Ország',

        currency: 'Alapértelmezett pénznem',

        continue: 'Folytatás',

        common: {
            title:
                'Tedd személyessé az Averiót',

            subtitle:
                'Néhány gyors lépéssel az Averiót ahhoz igazíthatjuk, ahogyan a pénzügyeidet kezeled.',

            step:
                '{{current}}. lépés / {{total}}',

            continue:
                'Folytatás',

            saving:
                'Mentés...',

            saved:
                'Mentve',

            skip:
                'Kihagyás egyelőre',

            finish:
                'Beállítás befejezése',
        },

        welcome: {
            eyebrow:
                'Üdv az Averióban',

            title:
                'Állítsuk be a pénzügyi otthonodat',

            description:
                'Az Averio a pénzügyeidet egy háztartás köré szervezi. Ez a háztartás kezdetben állhat csak belőled, később pedig bővítheted, ha valakivel közösen szeretnéd kezelni a pénzügyeiteket.',

            start:
                'Beállítás megkezdése',
        },

        profile: {
            title:
                'Mesélj egy kicsit magadról',

            description:
                'Ezekkel az adatokkal személyre szabhatjuk az Averiót. Később bármikor módosíthatod őket.',

            fullNameLabel:
                'Teljes név',

            fullNamePlaceholder:
                'A neved',

            countryLabel:
                'Ország',

            countryPlaceholder:
                'Válassz országot',

            genderLabel:
                'Nem',

            genderPlaceholder:
                'Válassz egy lehetőséget',

            genderMale:
                'Férfi',

            genderFemale:
                'Nő',

            genderOther:
                'Egyéb',

            genderPreferNotToSay:
                'Nem szeretném megadni',
        },

        household: {
            eyebrow:
                'A háztartásod',

            title:
                'Kezdd a háztartásoddal',

            description:
                'Minden Averio-fiók egy háztartáshoz tartozik. Kezdetben ez egyszerűen lehet a saját, személyes háztartásod.',

            householdOfOneTitle:
                'Kezdd egyedül',

            householdOfOneDescription:
                'Hozz létre egy háztartást csak magadnak. Később meghívhatsz valakit vagy csatlakozhatsz máshoz anélkül, hogy újra kellene építened a pénzügyeidet.',

            createTitle:
                'Hozd létre a háztartásodat',

            nameLabel:
                'Háztartás neve',

            namePlaceholder:
                'pl. Az én háztartásom',

            defaultName:
                'Az én háztartásom',

            helper:
                'A név csak arra szolgál, hogy könnyen azonosíthasd a háztartást az Averión belül.',

            privacyTitle:
                'Te döntöd el, mit osztasz meg',

            privacyDescription:
                'Egy közös háztartás nem jelenti automatikusan azt, hogy minden pénzügyi számládnak vagy tranzakciódnak közösnek kell lennie.',
        },

        firstAccount: {
            eyebrow:
                'Az első számlád',

            title:
                'Add hozzá a leggyakrabban használt számládat',

            description:
                'A számlák jelentik az Averio számára a kiindulópontot annak megértéséhez, hogy hol található a pénzed.',

            nameLabel:
                'Számla neve',

            namePlaceholder:
                'pl. Fő bankszámla',

            typeLabel:
                'Számla típusa',

            typePlaceholder:
                'Válassz számlatípust',

            currencyLabel:
                'Pénznem',

            currencyPlaceholder:
                'Válassz pénznemet',

            balanceLabel:
                'Jelenlegi egyenleg',

            balancePlaceholder:
                '0,00',

            balanceHelper:
                'Add meg a számlán jelenleg rendelkezésre álló összeget.',

            skipDescription:
                'Ezt a lépést kihagyhatod, és később is létrehozhatsz számlát.',
        },

        complete: {
            title:
                'Az Averio használatra kész',

            description:
                'A háztartásod be van állítva. Most már elkezdhetsz számlákat, tranzakciókat és célokat hozzáadni.',

            action:
                'Tovább az Averióhoz',
        },

        resume: {
            title:
                'Folytasd a beállítást',

            description:
                'Elmentettük az eddigi lépéseidet, így onnan folytathatod, ahol abbahagytad.',

            action:
                'Beállítás folytatása',
        },
    },

    navigation: {
        dashboard:
            'Áttekintés',

        household:
            'Háztartás',

        accounts:
            'Számlák',

        transactions:
            'Tranzakciók',

        recurringTransactions:
            'Ismétlődő tranzakciók',

        goals:
            'Célok',

        statistics:
            'Statisztikák',

        settings:
            'Beállítások',

        profile:
            'Profil',
    },

    dashboard: {
        title:
            'Áttekintés',

        greeting:
            'Üdv újra, {{name}}',

        householdOverview:
            'Háztartás áttekintése',

        currentBalance:
            'Jelenlegi egyenleg',

        monthlyIncome:
            'E havi bevétel',

        monthlyExpenses:
            'E havi kiadás',

        latestTransactions:
            'Legutóbbi tranzakciók',

        spendingByCategory:
            'Kiadások kategóriánként',

        viewAllTransactions:
            'Összes tranzakció megtekintése',

        empty: {
            title:
                'Itt kezdődik a pénzügyi áttekintésed',

            description:
                'Adj hozzá egy számlát és az első tranzakcióidat az áttekintés felépítéséhez.',
        },
    },

    household: {
        title:
            'Háztartás',

        description:
            'Kezeld a háztartásod tagjait és pénzügyi struktúráját.',

        members:
            'Tagok',

        member:
            'Tag',

        owner:
            'Tulajdonos',

        you:
            'Te',

        householdName:
            'Háztartás neve',

        createdAt:
            'Létrehozva',

        inviteMember:
            'Tag meghívása',

        leaveHousehold:
            'Kilépés a háztartásból',

        personalHousehold:
            'Személyes háztartás',

        sharedHousehold:
            'Közös háztartás',

        empty: {
            title:
                'A háztartásod veled kezdődik',

            description:
                'Használhatod az Averiót egyedül, és később meghívhatsz valakit, amikor közösen szeretnétek kezelni a pénzügyeiteket.',
        },
    },

    accounts: {
        title:
            'Számlák',

        description:
            'Nézd meg, hol található a háztartás pénze.',

        addAccount:
            'Számla hozzáadása',

        accountName:
            'Számla neve',

        accountType:
            'Számla típusa',

        currency:
            'Pénznem',

        balance:
            'Egyenleg',

        types: {
            checking:
                'Folyószámla',

            savings:
                'Megtakarítási számla',

            cash:
                'Készpénz',

            credit:
                'Hitelkártya',
        },

        empty: {
            title:
                'Még nincs számlád',

            description:
                'Add hozzá az első számládat, hogy elkezdhesd követni az egyenlegeket és tranzakciókat.',

            action:
                'Számla hozzáadása',
        },
    },

    transactions: {
        title:
            'Tranzakciók',

        addTransaction:
            'Tranzakció hozzáadása',

        income:
            'Bevétel',

        expense:
            'Kiadás',

        transfer:
            'Átvezetés',

        date:
            'Dátum',

        account:
            'Számla',

        category:
            'Kategória',

        amount:
            'Összeg',

        merchant:
            'Kereskedő',

        description:
            'Leírás',

        recurring:
            'Ismétlődő',

        latest:
            'Legutóbbi tranzakciók',

        empty: {
            title:
                'Még nincsenek tranzakciók',

            description:
                'A hozzáadott tranzakcióid itt fognak megjelenni.',
        },
    },

    goals: {
        title:
            'Célok',

        addGoal:
            'Cél hozzáadása',

        targetAmount:
            'Célösszeg',

        currentAmount:
            'Jelenlegi összeg',

        targetDate:
            'Céldátum',

        empty: {
            title:
                'Még nincsenek célok',

            description:
                'Hozz létre egy pénzügyi célt, és kövesd a háztartásod előrehaladását.',
        },
    },

    statistics: {
        title:
            'Statisztikák',

        income:
            'Bevételek',

        expenses:
            'Kiadások',

        balance:
            'Egyenleg',

        categories:
            'Kategóriák',

        period:
            'Időszak',

        thisMonth:
            'Ez a hónap',

        lastMonth:
            'Előző hónap',

        thisYear:
            'Ez az év',
    },

    validation: {
        required:
            'A mező kitöltése kötelező.',

        invalidEmail:
            'Adj meg egy érvényes e-mail-címet.',

        passwordTooShort:
            'A jelszónak legalább {{min}} karakterből kell állnia.',

        passwordsDoNotMatch:
            'A két jelszó nem egyezik.',

        invalidAmount:
            'Adj meg egy érvényes összeget.',

        amountMustBePositive:
            'Az összegnek nullánál nagyobbnak kell lennie.',

        nameTooLong:
            'A név legfeljebb {{max}} karakter hosszú lehet.',

        invalidSelection:
            'Válassz egy érvényes lehetőséget.',
    },

    errors: {
        genericTitle:
            'Valami hiba történt',

        genericDescription:
            'Nem sikerült végrehajtani a műveletet. Próbáld újra.',

        networkTitle:
            'Kapcsolati probléma',

        networkDescription:
            'Nem sikerült elérni az Averiót. Ellenőrizd az internetkapcsolatodat, majd próbáld újra.',

        unauthorized:
            'Nincs jogosultságod ehhez a művelethez.',

        forbidden:
            'Nincs hozzáférésed ehhez az erőforráshoz.',

        notFound:
            'A keresett erőforrás nem található.',

        conflict:
            'A művelet ütközik az adatok jelenlegi állapotával.',

        validation:
            'Néhány megadott adatot ellenőrizned kell.',
    },

    notifications: {
        changesSaved:
            'A módosításokat elmentettük.',

        accountCreated:
            'A számla sikeresen létrejött.',

        accountUpdated:
            'A számla sikeresen frissült.',

        householdCreated:
            'A háztartás sikeresen létrejött.',

        profileUpdated:
            'A profil sikeresen frissült.',

        transactionCreated:
            'A tranzakció sikeresen hozzáadva.',

        goalCreated:
            'A cél sikeresen létrejött.',
    },

    accessibility: {
        openMenu:
            'Menü megnyitása',

        closeMenu:
            'Menü bezárása',

        showPassword:
            'Jelszó megjelenítése',

        hidePassword:
            'Jelszó elrejtése',

        openLanguageSelector:
            'Nyelv módosítása',

        openThemeSelector:
            'Megjelenés módosítása',

        logoAlt:
            'Averio',
    },

    notFound: {
        code:
            '404',

        title:
            'Az oldal nem található',

        description:
            'A keresett oldal nem létezik, vagy időközben áthelyeztük.',

        action:
            'Vissza az áttekintéshez',
    },
} as const
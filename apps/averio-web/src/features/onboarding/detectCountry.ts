export function detectCountryCode():
    string {
    try {
        const locale =
            new Intl.Locale(
                navigator.language,
            );

        const region =
            locale.region
            ?? locale.maximize().region
            ?? '';

        return /^[A-Z]{2}$/.test(region)
            ? region
            : '';
    }
    catch {
        return '';
    }
}

export function suggestCurrency(
    countryCode: string,
): 'HUF' | 'EUR' | 'USD' {
    switch (
        countryCode.toUpperCase()
    ) {
        case 'HU':
            return 'HUF';

        case 'US':
            return 'USD';

        default:
            return 'EUR';
    }
}
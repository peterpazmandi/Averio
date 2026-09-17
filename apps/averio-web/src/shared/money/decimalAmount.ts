// Account storage supports 15 integer digits and four fractional digits.
export function normalizeBalanceInput(value: string): string | null {
    const normalized = value.trim().replace(',', '.');
    const match = /^(-?)([0-9]+)(?:\.([0-9]{1,4}))?$/.exec(normalized);

    if (!match) {
        return null;
    }

    const integer = match[2].replace(/^0+(?=[0-9])/, '');
    const fraction = (match[3] ?? '').replace(/0+$/, '');

    if (integer.length > 15) {
        return null;
    }

    const isZero = integer === '0' && fraction === '';
    const sign = isZero ? '' : match[1];

    return sign + integer + (fraction ? '.' + fraction : '');
}

export function negateDecimalAmount(value: string): string {
    if (value === '0') {
        return value;
    }

    return value.startsWith('-') ? value.slice(1) : '-' + value;
}

// Format the integer using BigInt; never convert the amount to Number.
// Totals can contain more than 15 integer digits, so there is no account limit here.
export function formatDecimalAmount(
    value: string,
    currency: string,
    language: string,
): string {
    const match = /^(-?)([0-9]+)(?:\.([0-9]{1,4}))?$/.exec(value);

    if (!match) {
        throw new Error('Invalid decimal amount returned by the API.');
    }

    const integer = BigInt(match[2]);
    const fraction = (match[3] ?? '').replace(/0+$/, '').padEnd(2, '0');
    const isNegative = match[1] === '-' && (integer !== 0n || /[1-9]/.test(fraction));

    // Negative zero is used only to obtain the locale's negative sign placement.
    const signedInteger = isNegative
        ? integer === 0n ? -0 : -integer
        : integer;

    const formatter = new Intl.NumberFormat(language, {
        style: 'currency',
        currency,
        numberingSystem: 'latn',
        minimumFractionDigits: 2,
        maximumFractionDigits: 4,
    });

    return formatter.formatToParts(signedInteger)
        .map((part) => part.type === 'fraction' ? fraction : part.value)
        .join('');
}

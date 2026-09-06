export interface PasswordStrength {
    score: 0 | 1 | 2 | 3 | 4;
    translationKey:
        | 'empty'
        | 'weak'
        | 'fair'
        | 'strong'
        | 'veryStrong';
}

export function calculatePasswordStrength(
    password: string,
): PasswordStrength {
    if (!password) {
        return {
            score: 0,
            translationKey: 'empty',
        };
    }

    let score = 0;

    if (password.length >= 8) {
        score += 1;
    }

    if (password.length >= 12) {
        score += 1;
    }

    const characterClasses = [
        /[a-z]/.test(password),
        /[A-Z]/.test(password),
        /\d/.test(password),
        /[^A-Za-z0-9]/.test(password),
    ].filter(Boolean).length;

    if (characterClasses >= 3) {
        score += 1;
    }

    if (
        characterClasses === 4
        || (
            password.length >= 16
            && characterClasses >= 3
        )
    ) {
        score += 1;
    }

    const normalizedScore =
        Math.min(
            score,
            4,
        ) as 0 | 1 | 2 | 3 | 4;

    const translationKey =
        normalizedScore <= 1
            ? 'weak'
            : normalizedScore === 2
                ? 'fair'
                : normalizedScore === 3
                    ? 'strong'
                    : 'veryStrong';

    return {
        score: normalizedScore,
        translationKey,
    };
}
import {
    useTranslation,
} from 'react-i18next';

import {
    calculatePasswordStrength,
} from '../password/passwordStrength';

interface Props {
    password: string;
}

export function PasswordStrengthIndicator({
    password,
}: Props) {
    const { t } =
        useTranslation();

    const strength =
        calculatePasswordStrength(
            password,
        );

    const activeClass =
        strength.score <= 1
            ? 'bg-red-500'
            : strength.score === 2
                ? 'bg-amber-500'
                : strength.score === 3
                    ? 'bg-primary-400'
                    : 'bg-primary-600';

    return (
        <div className="mt-3">
            <div
                className="
                    grid grid-cols-4 gap-1.5
                "
            >
                {[1, 2, 3, 4].map(
                    segment => (
                        <div
                            key={segment}
                            className={`
                                h-1.5
                                rounded-full
                                transition-all

                                ${
                                    segment <=
                                    strength.score
                                        ? activeClass
                                        : `
                                            bg-slate-200
                                            dark:bg-slate-700
                                        `
                                }
                            `}
                        />
                    ),
                )}
            </div>

            {password && (
                <p
                    className="
                        mt-2 text-xs
                        text-slate-500
                        dark:text-slate-400
                    "
                >
                    {t(
                        `password.strength.${strength.translationKey}`,
                    )}
                </p>
            )}
        </div>
    );
}
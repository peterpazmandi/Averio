import {
    createContext,
    useContext,
    useEffect,
    useState,
    type PropsWithChildren,
} from 'react';

export type Theme = 'light' | 'dark';

interface ThemeContextValue {
    theme: Theme;
    setTheme: (theme: Theme) => void;
    toggleTheme: () => void;
}

const ThemeContext =
    createContext<ThemeContextValue | undefined>(
        undefined,
    );

function getInitialTheme(): Theme {
    const stored =
        localStorage.getItem(
            'averio.theme',
        );

    if (
        stored === 'light'
        || stored === 'dark'
    ) {
        return stored;
    }

    return window.matchMedia(
        '(prefers-color-scheme: dark)',
    ).matches
        ? 'dark'
        : 'light';
}

export function ThemeProvider({
    children,
}: PropsWithChildren) {
    const [theme, setThemeState] =
        useState<Theme>(
            getInitialTheme,
        );

    function setTheme(
        nextTheme: Theme,
    ) {
        setThemeState(
            nextTheme,
        );
    }

    function toggleTheme() {
        setTheme(
            theme === 'dark'
                ? 'light'
                : 'dark',
        );
    }

    useEffect(
        () => {
            document.documentElement
                .classList.toggle(
                    'dark',
                    theme === 'dark',
                );

            document.documentElement
                .style.colorScheme =
                    theme;

            localStorage.setItem(
                'averio.theme',
                theme,
            );
        },
        [theme],
    );

    return (
        <ThemeContext.Provider
            value={{
                theme,
                setTheme,
                toggleTheme,
            }}
        >
            {children}
        </ThemeContext.Provider>
    );
}

export function useTheme():
    ThemeContextValue {
    const context =
        useContext(ThemeContext);

    if (!context) {
        throw new Error(
            'useTheme must be used inside ThemeProvider.',
        );
    }

    return context;
}
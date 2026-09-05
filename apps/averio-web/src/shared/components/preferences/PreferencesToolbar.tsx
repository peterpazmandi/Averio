import { LanguageSwitcher } from '../../../features/system/components/LanguageSwitcher';
import { ThemeToggle } from './ThemeToggle';

export function PreferencesToolbar() {
    return (
        <div className="flex items-center gap-2">
            <LanguageSwitcher />
            <ThemeToggle />
        </div>
    );
}
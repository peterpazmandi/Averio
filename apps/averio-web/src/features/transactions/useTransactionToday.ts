import { useEffect, useState } from 'react';
export function useTransactionToday() {
    const [today, setToday] = useState(() => new Date().toISOString().slice(0, 10));
    useEffect(() => {
        const refresh = () => setToday(new Date().toISOString().slice(0, 10));
        const timer = window.setInterval(refresh, 30_000);
        window.addEventListener('focus', refresh);
        document.addEventListener('visibilitychange', refresh);
        return () => { window.clearInterval(timer); window.removeEventListener('focus', refresh); document.removeEventListener('visibilitychange', refresh); };
    }, []);
    return new Date(today + 'T12:00:00Z');
}

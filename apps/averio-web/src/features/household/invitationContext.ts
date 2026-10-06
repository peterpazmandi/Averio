const key = 'averio.pendingInvitation';
const lifetime = 7 * 24 * 60 * 60 * 1000;
export function pendingInvitation(): string | null {
    try {
        const value = JSON.parse(sessionStorage.getItem(key) ?? 'null');
        return value && /^[A-Fa-f0-9]{64}$/.test(value.token) && value.expires > Date.now() ? value.token : null;
    } catch { return null; }
}
export function rememberInvitation(token: string) {
    if (!/^[A-Fa-f0-9]{64}$/.test(token)) return;
    try { sessionStorage.setItem(key, JSON.stringify({ token, expires: Date.now() + lifetime })); } catch { /* Reopen the link if storage is unavailable. */ }
}
export function forgetInvitation() { try { sessionStorage.removeItem(key); } catch { /* No persisted context. */ } }

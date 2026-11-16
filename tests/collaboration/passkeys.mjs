import assert from 'node:assert/strict';

export async function verifyPasskeys({ session, fixture, otherFixture, password, api, ok, control, ui }) {
    const page = await session(fixture.email, password);
    const cdp = await page.context().newCDPSession(page);
    await cdp.send('WebAuthn.enable');
    await cdp.send('WebAuthn.addVirtualAuthenticator', {
        options: {
            protocol: 'ctap2', transport: 'internal', hasResidentKey: true,
            hasUserVerification: true, isUserVerified: true, automaticPresenceSimulation: true,
        },
    });

    await page.goto(ui + '/app/profile#profile-passkeys');
    await page.getByRole('heading', { name: 'Passkeys' }).waitFor();
    await page.getByRole('textbox', { name: 'Name this passkey' }).fill('Test device');
    await page.getByLabel('Confirm your password').fill(password);
    await page.getByRole('button', { name: 'Add a passkey' }).click();
    await page.getByText('Passkey added.', { exact: true }).waitFor();
    assert.equal((await ok(page, '/profile/passkeys')).length, 1);

    await page.getByRole('button', { name: 'Sign out', exact: true }).click();
    await page.waitForURL('**/login');
    await page.getByRole('button', { name: 'Sign in with a passkey' }).click();
    await page.waitForURL('**/app');
    assert.equal((await ok(page, '/auth/me')).email, fixture.email);

    await page.goto(ui + '/app/profile#profile-passkeys');
    await page.getByText('Test device', { exact: true }).waitFor();
    await page.getByRole('button', { name: 'Remove', exact: true }).click();
    await page.getByLabel('Confirm your password').fill(password);
    await page.getByRole('button', { name: 'Remove', exact: true }).last().click();
    await page.getByText('Passkey removed.', { exact: true }).waitFor();
    assert.deepEqual(await ok(page, '/profile/passkeys'), []);

    // Removal changes the security stamp. The browser that removed the key
    // must retain a fresh cookie after the stamp-validation interval.
    await control('/advance', { seconds: 6 * 60 });
    assert.equal((await ok(page, '/auth/me')).email, fixture.email);

    // Authenticated accounts must not share the passkey request budget merely
    // because their traffic arrives through the same proxy address.
    const other = await session(otherFixture.email, password);
    for (let attempt = 0; attempt < 6; attempt++)
        await ok(page, '/profile/passkeys/options', { method: 'POST', body: { password } });
    for (let attempt = 0; attempt < 8; attempt++)
        await ok(other, '/profile/passkeys/options', { method: 'POST', body: { password } });

    // Five bad confirmations lock this account even with a valid session.
    for (let attempt = 0; attempt < 5; attempt++)
        assert.equal((await api(page, '/profile/passkeys/options', { method: 'POST', body: { password: 'incorrect' } })).status, 401);
    assert.equal((await api(page, '/profile/passkeys/options', { method: 'POST', body: { password } })).status, 401);
    console.log('PASS passkey enrollment, sign-in, revocation, session refresh, account throttling and lockout');
}

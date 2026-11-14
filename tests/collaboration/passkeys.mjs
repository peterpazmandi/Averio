import assert from 'node:assert/strict';

export async function verifyPasskeys({ session, fixture, password, ok, ui }) {
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
    console.log('PASS passkey enrollment, passwordless sign-in and revocation');
}

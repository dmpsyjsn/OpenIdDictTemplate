const $ = (id) => document.getElementById(id);

function render(user) {
    $('profile').innerText = user ? JSON.stringify(user.profile, null, 2) : '(not signed in)';
    $('expiry').innerText = user ? new Date(user.expires_at * 1000).toString() : '';
    $('access-token').innerText = user ? user.access_token : '';
}

(async () => {
    const { userManager, config } = await createUserManager();

    $('login-button').onclick = () => userManager.signinRedirect();
    $('logout-button').onclick = () => userManager.signoutRedirect();

    // Uses the refresh token (offline_access) rather than a hidden iframe.
    $('refresh-button').onclick = async () => render(await userManager.signinSilent());

    $('api-button').onclick = async () => {
        const user = await userManager.getUser();
        if (!user) {
            $('api-response').innerText = 'Login first.';
            return;
        }

        const response = await fetch(`${config.ApiBaseUrl}/identity`, {
            headers: { Authorization: `Bearer ${user.access_token}` },
        });
        $('api-response').innerText = `${response.status}\n${await response.text()}`;
    };

    render(await userManager.getUser());
})();

oidc.Log.setLogger(console);

// Loads /config.json (served from appsettings "Oidc") and builds the UserManager.
async function createUserManager() {
    const config = await (await fetch('/config.json')).json();

    const userManager = new oidc.UserManager({
        authority: config.Authority,
        client_id: config.ClientId,
        scope: config.Scope,
        redirect_uri: config.RedirectUri,
        post_logout_redirect_uri: config.PostLogoutRedirectUri,
        response_type: 'code',
        userStore: new oidc.WebStorageStateStore({ store: window.localStorage }),
    });

    return { userManager, config };
}

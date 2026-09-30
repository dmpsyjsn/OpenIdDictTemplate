# OpenIdDictTemplate

A copy-from template for adding login, an OpenID Connect server and token-protected APIs to a .NET 10 solution. It uses [OpenIddict](https://github.com/openiddict/openiddict-core) and ASP.NET Core Identity.

It provides:

- An OpenIddict server with authorization code (PKCE) and refresh token flows, introspection, userinfo and end-session endpoints.
- MVC login, logout, consent, forgot/reset password and email confirmation pages.
- An admin API to invite users, secured by an `Admin` role plus a scope.
- A sample resource API that validates tokens by introspection.
- A browser test client built on oidc-client-ts.
- EF Core persistence with real migrations for **Sqlite**, **SQL Server** or **PostgreSQL**, switched by configuration.

## Layout

```
OpenIdDictTemplate/
  OpenIdDictTemplate.slnx
  OpenIdDictTemplate.Security.Host/                   OIDC server, controllers, views
  OpenIdDictTemplate.Security.Logic/                  DbContext, CQRS plumbing, user commands, email, seeding
  OpenIdDictTemplate.Security.Migrations.Sqlite/      EF migrations (Sqlite)
  OpenIdDictTemplate.Security.Migrations.SqlServer/   EF migrations (SQL Server)
  OpenIdDictTemplate.Security.Migrations.Postgres/    EF migrations (PostgreSQL)
  OpenIdDictTemplate.Security.Testing.ResourceApi/    sample API protected by the server
  OpenIdDictTemplate.Security.Testing.JavascriptClient/  browser test client
```

| App | https | http |
|---|---|---|
| Host (issuer) | 7017 | 5094 |
| JavascriptClient | 7041 | 5227 |
| ResourceApi | 7063 | 5063 |

## Running it

Prerequisites: the .NET 10 SDK, and a trusted dev certificate (`dotnet dev-certs https --trust`).

1. Set the development secrets. The seeded admin password and the resource API's client secret are deliberately not in source control:

   ```
   cd OpenIdDictTemplate.Security.Host
   dotnet user-secrets set "Seed:Users:admin:Password" "<a strong password>"
   dotnet user-secrets set "Seed:Clients:resource_api:ClientSecret" "<a secret>"

   cd ../OpenIdDictTemplate.Security.Testing.ResourceApi
   dotnet user-secrets set "Introspection:ClientSecret" "<the same secret>"
   ```

2. Start the three projects (each has an `https` launch profile):

   ```
   dotnet run --project OpenIdDictTemplate.Security.Host
   dotnet run --project OpenIdDictTemplate.Security.Testing.ResourceApi
   dotnet run --project OpenIdDictTemplate.Security.Testing.JavascriptClient
   ```

3. Open https://localhost:7041, choose **Login** and sign in as `admin@example.com`. You can then refresh the token, call the API (`/identity`) and log out. The page also shows the access token, which you can paste into the Host's `.http` file to try the admin endpoints.

On first start the Host applies migrations and seeds the roles, users, scopes and clients from the `Seed` section (development only). Seeding is an idempotent upsert: restarts don't duplicate or rewrite anything, and unknown clients are never deleted.

In Development, emails are written as `.eml` files to `OpenIdDictTemplate.Security.Host/App_Data/Emails` instead of being sent. Open one to get the confirmation or reset link.

## Configuration

All Host settings are in `appsettings.json` (defaults) and `appsettings.Development.json` (seed data, dev certificates, CORS).

| Section | Purpose |
|---|---|
| `Database` | `Provider` (`Sqlite`, `SqlServer` or `Postgres`) and `ApplyMigrationsOnStartup`. Connection strings are under `ConnectionStrings:Sqlite` / `:SqlServer` / `:Postgres`. |
| `Security` | `Issuer`, `DefaultRedirectUrl`, `AccessTokenLifetime`, `CorsOrigins`, and `Certificates` (development certificates, or PFX paths and passwords for production). |
| `Email` | `Type` (`PickupDirectory` or `Smtp`), sender details, `ProductName` (used in email templates), SMTP settings. |
| `Seed` | Roles, users, scopes and clients, as keyed dictionaries so individual values can be overridden from user-secrets. |

Client permissions are derived from the seed definition: grant types give the endpoints and response types, post-logout URIs give `EndSession`, and scopes become `scp:` permissions.

**Invariant:** a scope's `Resources` entry must equal the resource API's introspection client id (`resource_api`). Otherwise introspection returns no claims.

### Switching database

Set `Database:Provider` (for example with the environment variable `Database__Provider=Postgres`). The default Postgres connection string targets `localhost:5432` with user `postgres`; override `ConnectionStrings:Postgres` (ideally in user-secrets) for your server. Each provider has its own migrations assembly, so the single `ApplicationDbContext` stays provider-agnostic.

### Adding a migration

The `dotnet-ef` version is pinned in `.config/dotnet-tools.json`. Generate the migration for **all three** providers, and pass the provider by environment variable:

```
dotnet tool restore
Database__Provider=Sqlite    dotnet tool run dotnet-ef migrations add <Name> --project OpenIdDictTemplate.Security.Migrations.Sqlite    --startup-project OpenIdDictTemplate.Security.Host
Database__Provider=SqlServer dotnet tool run dotnet-ef migrations add <Name> --project OpenIdDictTemplate.Security.Migrations.SqlServer --startup-project OpenIdDictTemplate.Security.Host
Database__Provider=Postgres  dotnet tool run dotnet-ef migrations add <Name> --project OpenIdDictTemplate.Security.Migrations.Postgres  --startup-project OpenIdDictTemplate.Security.Host
```

(In PowerShell, set `$env:Database__Provider = 'Sqlite'` first.)

## Security notes

- **Admin API:** `/admin/users/*` requires the `SecurityAdmin` policy: a valid token, the `Admin` role and the `security_admin` scope.
- **Production:**
  - Set `UseDevelopmentCertificates` to `false` and provide real encryption and signing PFX files.
  - Set `Security:Issuer` to the public HTTPS URL.
  - Keep `DisableTransportSecurityRequirement` off.
  - Don't ship the seed section, or at least not its passwords or secrets.
  - Use the `Smtp` email type.
- **Password reset** never reveals whether an email exists, and the account-cancel link needs a POST so email scanners can't delete accounts.

## Using it as a template

Copy the Host, Logic and Migrations projects and rename the namespaces. The `Testing` projects are samples for trying the server and can be left behind. The CQRS pieces in `Logic/Abstractions` are intentionally small (handlers, decorators for logging and unit of work) and use plain Microsoft DI.

## License

Apache License 2.0. See [LICENSE](LICENSE).

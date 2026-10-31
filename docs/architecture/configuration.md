# Configuration and secrets

Non-secret defaults stay in API `appsettings.json` and `appsettings.Development.json`.
Secrets live in an ignored `.env` at repository root. Copy `.env.example` for a new checkout, then supply real values locally. `__` separates configuration sections.

The API loads `.env` before creating its host, searching application parent
directories. This supports IDE launches, `dotnet run`, and EF CLI startup.
Existing process environment variables override `.env`; command-line configuration
keeps normal ASP.NET Core precedence. Variable expansion is disabled to preserve
literal `$` in credentials. Quote secret values with single quotes.

Docker Compose injects `.env` through `env_file`. Dotenv files are excluded from
Docker build context and are not copied to published application output. For
production deployments, inject equivalent environment variables through the
platform's secret store; do not bake credentials into images.

Secret keys:

- `ConnectionStrings__DefaultConnection`
- `ConnectionStrings__Debug` (optional alternate connection)
- `DefaultAdmin__Password`
- `EmailService__ApiKey` (Resend)
- `EmailService__Smtp__Username`
- `EmailService__Smtp__Password`

Only selected email provider needs credentials. Choose provider and SMTP endpoint
in appsettings, or override settings using environment variables such as
`EmailService__Provider=Smtp`.

Account setup link (non-secret) is configured under `AccountSetup`:

- `AccountSetup:FrontendBaseUrl` — absolute `http(s)` origin of the frontend used in the password setup email. Empty in `appsettings.json`; `http://localhost:3000` in `appsettings.Development.json`. Set `AccountSetup__FrontendBaseUrl` in every deployed environment.
- `AccountSetup:SetPasswordPath` — frontend route, default `/set-password`. The link is `{FrontendBaseUrl}{SetPasswordPath}?email=…&token=…`.

An invalid or empty value is reported when the first setup email is sent; the create/resend request fails and rolls back.

# SAMYR TECH WAY — Spin & Win

Promotional spin-the-wheel site for SAMYR TECH WAY laptop buyers. Staff create a customer in `/admin` and hand over a one-time
**access code**. The customer signs in with **phone + access code** and spins with their **Spin Credits**. The server picks every
result. "Try Again" gives the credit back in the same SQL transaction, and a real prize creates an award that staff mark as
delivered.

- **Stack:** ASP.NET Core 10 MVC + Razor, EF Core 10 on SQL Server, ASP.NET Core Identity (admins only), vanilla ES modules + GSAP
  (bundled locally). No Node, Docker, Redis or background workers are needed in production.
- **Hosting target:** MonsterASP.NET Free (Windows/IIS in-process + MSSQL). See [docs/MONSTERASP_DEPLOY.md](docs/MONSTERASP_DEPLOY.md).
- **Design & decisions:** [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md). Manual QA: [docs/TEST_CHECKLIST.md](docs/TEST_CHECKLIST.md).

```
src/SamyrTechWay.SpinWin/        web app (Controllers, Areas/Admin, Services, Data, Views, wwwroot)
tests/SamyrTechWay.SpinWin.Tests/ xUnit v3: unit, spin engine, concurrency, HTTP security (SQLite) + opt-in SQL Server suite
tests/js/                         node:test for the wheel target-angle maths
deploy/sql/migrate-idempotent.sql reviewed, re-runnable schema script for production
tools/                            dev-only helpers (SQL script generator, icon generator)
```

## 1. Prerequisites (macOS, Apple Silicon or Intel)

- .NET SDK 10 (`dotnet --version` → 10.0.x), VS Code with the C# Dev Kit, Git.
- A SQL Server you can reach from the Mac. Pick one:
  - **A. MonsterASP MSSQL with Remote access enabled.** Use the *Remote* connection string from the Monster panel. This is the
    simplest option and needs no local install.
  - **B. A local SQL Server 2022 container (dev only).** For example with [colima](https://github.com/abiosoft/colima) on Apple
    Silicon: `colima start --arch x86_64 --vm-type vz --vz-rosetta --memory 4`, then
    `docker run -d --name stw-mssql -e ACCEPT_EULA=Y -e MSSQL_SA_PASSWORD="$SA_PW" -p 14333:1433 mcr.microsoft.com/mssql/server:2022-latest`.
- Optional: Node 20+ (only to run `tests/js`).

```bash
cd ~/Projects/samyr-tech-way-spinwin
dotnet tool restore          # installs the pinned dotnet-ef 10.0.12 local tool
dotnet restore
```

## 2. Secrets (never in Git)

Configuration comes from `appsettings.json` (no secrets), then **user-secrets** (Development only), then **environment variables**.
Use `read -s` so passwords never land in your shell history (the commands below use zsh syntax, the macOS default shell):

```bash
P=src/SamyrTechWay.SpinWin
read -s "CS?SQL connection string: "; echo
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "$CS" --project $P; unset CS
```

Example shape: `Server=<host>,<port>;Database=<db>;User Id=<user>;Password=<pw>;TrustServerCertificate=True;Encrypt=True`.
`TrustServerCertificate=True` is only for a local container. With Monster, use the exact string their panel gives you.

## 3. Create the database schema

The migration `InitialCreate` is already in `src/SamyrTechWay.SpinWin/Data/Migrations`. Do **not** add it again.

```bash
dotnet ef database update --project src/SamyrTechWay.SpinWin
```

Or run [deploy/sql/migrate-idempotent.sql](deploy/sql/migrate-idempotent.sql) in any SQL console. It is safe to re-run. After you
add a new migration, regenerate it with `./tools/make-sql-script.sh`.

## 4. Create the first administrator (secure bootstrap)

There is no registration page. The app creates the admin at startup from `AdminBootstrap:*` settings, and you remove them again
straight after:

```bash
P=src/SamyrTechWay.SpinWin
dotnet user-secrets set "AdminBootstrap:Email" "you@yourcompany.com" --project $P
read -s "PW?Admin password (min 12 chars, upper+lower+digit): "; echo
dotnet user-secrets set "AdminBootstrap:Password" "$PW" --project $P; unset PW
dotnet run --project $P --launch-profile http        # log: "Bootstrap admin … created. Remove AdminBootstrap__Password …"
# Ctrl+C, then:
dotnet user-secrets remove "AdminBootstrap:Password" --project $P
```

- If the admin already exists, the bootstrap settings are ignored and a reminder is logged.
- **Forgotten password / locked out:** set `AdminBootstrap:Password` to a new value plus `AdminBootstrap:ResetPasswordIfExists=true`,
  start the app once, then remove both settings. The reset is one-shot. It is audited, and once the admin has signed in after it,
  later restarts won't reset the password again, even if the settings were left in place.
- **A second admin:** run the bootstrap again with a different `AdminBootstrap:Email`.
- Signed-in admins can change their password at `/admin/account/password`.

## 5. Run locally

```bash
dotnet run --project src/SamyrTechWay.SpinWin --launch-profile http
```

- Customer site: **http://localhost:5137**. Arabic by default; the English toggle is in the header.
- Admin: **http://localhost:5137/admin**
- Health: http://localhost:5137/health

In `Development` the app seeds an editable sample prize catalogue (`Database:SeedDemoPrizes`, ignored outside Development) and
does not force HTTPS. If you prefer HTTPS, run `dotnet dev-certs https --trust` and use `--launch-profile https`
(https://localhost:7034).

## 6. First prizes and first customer

1. `/admin/prizes`: set each prize's name (AR + EN), icon or photo (JPEG/PNG/WebP ≤ 2 MB), colour, celebration tier and optional
   stock. The **Try Again** slice is built in.
2. In the **allocation** table, the active slices must add up to exactly **100 %** before you can save. Effective odds (after
   sold-out items drop out) show next to each row, with a live preview of the wheel.
3. `/admin/customers` → **New customer**: name, phone (`010…`, `+20…` or `0020…`), optional invoice and laptop model, and initial
   credits. The access code is shown **once**. Copy it or print the card. Codes are stored hashed and can't be shown again; use
   **Reset code** to issue a new one, which also signs out the old session.
4. The customer opens the site, enters phone + code and spins. Real wins appear under `/admin/awards`. Mark them **Delivered** when
   handed over.
5. Use `/admin/customers/{id}` to grant or revoke credits (each change needs a reason and is written to the ledger). Use
   `/admin/settings` to edit texts, pause the campaign or tune animation and sound.

## 7. Tests

```bash
dotnet test -c Release                                # unit + spin engine + concurrency + HTTP security, on SQLite
node --test tests/js/wheel-math.test.mjs              # wheel landing-angle maths
```

The SQL Server suite (real row locks, rowversion and filtered indexes) runs against a throwaway database that it creates and drops
itself. It needs a login allowed to `CREATE DATABASE`, so never point it at production:

```bash
read -s "TCS?Test SQL Server (no Database=): "; echo
SPINWIN_TEST_SQLSERVER="$TCS" dotnet test -c Release; unset TCS
```

Without that variable the 27 SQL Server tests show as **skipped**, not passed.

## 8. Publish

```bash
dotnet publish src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj -c Release -o ./publish
```

This is a framework-dependent, portable build with `web.config` for IIS in-process. `appsettings.Development.json` is not published.
Deployment steps are in [docs/MONSTERASP_DEPLOY.md](docs/MONSTERASP_DEPLOY.md).

## Configuration reference

| Key (env var form) | Default | Purpose |
|---|---|---|
| `ConnectionStrings__DefaultConnection` | — (required) | SQL Server connection string. The app refuses to start without it. |
| `ASPNETCORE_ENVIRONMENT` | `Production` | Use `Production` on the host. |
| `Security__RequireHttps` | `true` | HTTPS redirect + HSTS + Secure-only cookies. Set to `false` **only** before HTTPS is active (a warning banner shows). |
| `Security__TrustForwardedHeaders` | `false` | Honour `X-Forwarded-*`. Only enable behind a proxy you trust. |
| `Security__KnownProxies` | — | Optional comma-separated proxy IPs allowed to send `X-Forwarded-*` (with the setting above). |
| `Database__ApplyMigrationsOnStartup` | `false` | Apply pending migrations at startup. Back up first. |
| `AdminBootstrap__Email` / `__Password` / `__DisplayName` / `__ResetPasswordIfExists` | — | One-off admin creation or recovery. Remove after use. |
| `RateLimiting__LoginPerMinute` / `__SpinPerMinute` | `10` / `20` | Per-IP login and per-customer spin limits. |

## Licences

GSAP 3.15.0 (standard "no charge" GSAP licence, see `wwwroot/lib/gsap/LICENSE-NOTE.txt`), Lucide icons (ISC), Outfit and Cairo
fonts (SIL OFL 1.1). The SAMYR TECH WAY logo belongs to its owner and is used unchanged.
# el-mostawred

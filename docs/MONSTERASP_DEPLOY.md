# Deploy to MonsterASP.NET Free (GitHub → Git deploy), from macOS

This runbook is for this repository as built. Steps marked **[account]** happen in your MonsterASP or GitHub account. They are not
automated or verified from the repo. Read every step before starting.

MonsterASP reference docs:
- https://help.monsterasp.net/getting-started/first-steps/free-vs-premium
- https://help.monsterasp.net/websites/git-deploy/github-repository
- https://help.monsterasp.net/websites/configuration/environment-variables
- https://help.monsterasp.net/databases/mssql/create-database
- https://help.monsterasp.net/databases/mssql/connection-string
- https://help.monsterasp.net/databases/mssql/remote-access
- https://help.monsterasp.net/domains-https/https/lets-encrypt
- https://help.monsterasp.net/websites/deploy/ftp-sftp

## What the app needs from the host

| Need | How this app meets it |
|---|---|
| Windows IIS, in-process | Standard framework-dependent `dotnet publish`, auto-generated `web.config` (`AspNetCoreModuleV2`, `inprocess`). |
| No writable disk | Prize photos **and** Data Protection keys are stored in SQL, so redeploys and recycles lose nothing. |
| Idle shutdown (~3 h) | No timers, workers or in-memory critical state. Rate-limit counters reset on recycle, which is harmless. |
| Secrets | Only environment variables. `appsettings.json` holds no secrets, and `appsettings.Development.json` is not published. |

## 0. Before you start (on the Mac)

```bash
dotnet build -c Release && dotnet test -c Release        # must be green
git init -b main                                         # only if this folder isn't a repo yet
git add -A && git status                                 # review the list
git grep -nIiE "password=|pwd=|user id=sa" -- . ':!docs' ':!README.md' || echo "no connection strings in tracked files"
```

`.gitignore` already excludes `bin/`, `obj/`, `publish/`, `*.db`, `appsettings.*.local.json` and similar files. user-secrets live
outside the repo (`~/.microsoft/usersecrets`). Commit, then push to a **private** GitHub repository **[account]**.

## 1. Create the website and database **[account]**

1. On https://www.monsterasp.net/ create a **Free** ASP.NET Core website and note the `*.runasp.net` address.
2. **Databases → Create MSSQL** on the same Free plan.
3. Copy **two** connection strings from the database page:
   - **Local** (internal): used by the website on Monster.
   - **Remote** (public): only needed if you run migrations from the Mac. It requires **Remote access → Enabled**.

## 2. Create the schema (pick one)

Back up first if the database already holds data. Every option is safe to repeat.

- **A. SQL script (recommended).** Open Monster's database web console (WebMSSQL / *Run T-SQL*), paste the full contents of
  [`deploy/sql/migrate-idempotent.sql`](../deploy/sql/migrate-idempotent.sql) and run it. It creates 13 tables plus
  `__EFMigrationsHistory`, and skips anything already applied. (Verified locally: applied twice to SQL Server 2022 without errors.)
- **B. From the Mac.** Temporarily enable **Remote access**, then:
  ```bash
  read -s "CS?Monster REMOTE connection string: "; echo
  ConnectionStrings__DefaultConnection="$CS" dotnet ef database update --project src/SamyrTechWay.SpinWin; unset CS
  ```
  Afterwards, turn Remote access **off** again.
- **C. At first startup.** Set `Database__ApplyMigrationsOnStartup=true` in step 3, then **remove it** after the first successful
  start. If the schema is behind, the app logs a critical message on start.

## 3. Environment variables **[account]**

Website → **Environment variables**, then restart the site after every change:

| Variable | Value | Keep? |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | yes |
| `ConnectionStrings__DefaultConnection` | the **Local** connection string | yes |
| `AdminBootstrap__Email` | your admin e-mail | remove after step 5 |
| `AdminBootstrap__Password` | strong password (≥ 12 chars, upper + lower + digit) | **remove after step 5** |
| `AdminBootstrap__DisplayName` | e.g. `Samyr Admin` (optional) | remove after step 5 |
| `Security__RequireHttps` | `false` **only until HTTPS is active** (step 6) | remove after step 6 |

Leave `Security__TrustForwardedHeaders` unset unless step 6 shows a redirect loop.

## 4. Git deploy **[account]**

Website → **Git deploy**:

1. Repository URL `https://github.com/<owner>/<repo>.git`. For a private repo, use a fine-grained GitHub token with read-only
   *Contents* access to this repository only.
2. Branch `main` → **Build .NET app** → project `src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj` → enable.
3. Click **Pull latest**, then **Build & publish**, and check *Recent builds* for errors.
4. On the Free plan **pushes are not deployed automatically**. Repeat *Pull latest → Build & publish* after each push.

## 5. First start and admin bootstrap

1. Open `http://<site>.runasp.net/health`. You should see `Healthy`. The first request after idle can take a few seconds.
2. The admin from step 3 is created on startup. Open `/admin/login` and sign in.
3. **Now delete `AdminBootstrap__Password`** (and the other `AdminBootstrap__*` variables), then restart. If the variables stay, the
   app ignores them for an existing admin and logs a reminder, but they should not stay on the server.
4. To recover a locked-out admin later, set `AdminBootstrap__Email`, a new `AdminBootstrap__Password` and
   `AdminBootstrap__ResetPasswordIfExists=true`, restart once, sign in, then remove all three. The reset is one-shot: once you've
   signed in after it, restarts won't reset the password again.

## 6. HTTPS (Free plan) **[account]**

1. While on plain HTTP (`Security__RequireHttps=false`), every customer and admin page shows a striped **"Demo connection — secure HTTPS is not active yet"** warning banner.
   Use test customers only.
2. Open a **support ticket** asking Monster to activate HTTPS (Let's Encrypt) for your `*.runasp.net` site. On Free this happens
   only after the first upload.
3. When `https://<site>.runasp.net` works, **delete `Security__RequireHttps`** (the default is `true`) and restart. HTTP now
   redirects to HTTPS, HSTS is sent, cookies become Secure-only and the banner disappears.
4. If you then get a redirect loop, Monster terminates TLS before the app. Set `Security__TrustForwardedHeaders=true` and restart.
   If Monster support can tell you the proxy's IP, also set `Security__KnownProxies=<ip>` so only that proxy is trusted.

## 7. Go-live checks

Run [docs/TEST_CHECKLIST.md § Post-deploy](TEST_CHECKLIST.md#post-deploy-on-monsterasp). At minimum, check:
admin login → create customer → copy code → customer login → spin (Try Again path and a win) → refresh shows the same result →
admin sees the attempt and award → grant a credit → mark delivered → mobile → share PNG → `/health` after a restart.

Then remove any test customers, set the real prize allocation, and turn SQL **Remote access off**.

## 8. Updating later

- **Code only:** push → *Pull latest* → *Build & publish*.
- **Schema change:** on the Mac, `dotnet ef migrations add <Name> --project src/SamyrTechWay.SpinWin`, then
  `./tools/make-sql-script.sh`, then review the diff of `deploy/sql/migrate-idempotent.sql`. **Back up**, run the script on Monster
  (step 2A), *then* deploy the code. Never drop or recreate a live database.
- **Backups:** the Free plan has **no automatic backups**. Before each schema change, and on a regular schedule, export or back up
  the database from the Monster panel.

## Alternative: manual publish over SFTP/FTPS

```bash
dotnet publish src/SamyrTechWay.SpinWin/SamyrTechWay.SpinWin.csproj -c Release -o ./publish
```

Upload the **contents** of `publish/` (not the source) to the host's `/wwwroot`, using the credentials from Website → Deploy.
Static files end up in `/wwwroot/wwwroot`, which is expected. Stop the site (or upload `app_offline.htm`) before overwriting DLLs.
Nothing on the server's disk needs preserving between deploys.

The portable output is ~80 MB, mostly SqlClient's native auth helpers for other operating systems. If your Monster app pool is
64-bit, `-r win-x64 --self-contained false` gives ~18 MB. This is optional and has not been verified on Monster.

## Free plan limits (as documented by MonsterASP, Oct 2026)

~256 MB RAM, 1 GB MSSQL, capped traffic, EU datacenter, no AlwaysRunning (idle stop after ~3 h, so the first request is slow),
archive after ~6 months unused, no automatic daily backups, HTTPS by support request. This is fine for a promotion. It is not a
production SLA.

## Troubleshooting

| Symptom | Check |
|---|---|
| **500.30** / app won't start | Monster logs. The usual cause is a missing `ConnectionStrings__DefaultConnection`; the app stops with a message naming that variable. Also check the .NET 10 runtime selection and `ASPNETCORE_ENVIRONMENT`. |
| Pages work but DB errors | You used the Remote string on the host. Use the **Local** one. Also check the schema step ran (`__EFMigrationsHistory` contains `20261012005845_InitialCreate`). |
| Can't sign in on HTTP | `Security__RequireHttps` is still `true` before HTTPS exists, so Secure-only cookies are dropped. Set it to `false` until step 6. |
| Redirect loop after HTTPS | Set `Security__TrustForwardedHeaders=true`. |
| Admin bootstrap didn't happen | Log line `Bootstrap admin … could not be created` shows the password rule that failed (≥ 12 chars, upper, lower, digit). |
| 403 / directory listing | You uploaded source instead of `publish/` output, or used the wrong folder. |
| Users signed out after every restart | Data Protection keys can't be saved. Check that table `DataProtectionKeys` exists (schema step). |

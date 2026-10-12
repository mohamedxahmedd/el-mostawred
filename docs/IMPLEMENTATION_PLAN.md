# SAMYR TECH WAY — Spin & Win · Implementation Plan

Authoritative spec: `docs/MASTER_BUILD_PROMPT.md`. This plan records the decisions taken and tracks progress.

## 1. Starting point (inspected)

- `SamyrTechWay.SpinWin.slnx` with the default `dotnet new mvc` web project (net10.0) and an empty xUnit test project.
- .NET SDK 10.0.401 on macOS arm64. No SQL Server installed locally; Docker/colima available but stopped.
- Logo: `wwwroot/assets/brand/samyr-tech-way-logo.png`, 1254×1254 RGBA transparent PNG. Ruby → flame → amber → gold gradients;
  the small "EL-MOSTAWRYD" and "Laptop Trading" lines are **dark grey**, so the full logo is always shown on a light
  champagne "plaque" panel when it sits on black. The file is used unchanged.

## 2. Key decisions

| Area | Decision | Why |
|---|---|---|
| Structure | One MVC web project + one test project. Folders: `Controllers`, `Areas/Admin`, `Models/Entities`, `Models/ViewModels`, `Data`, `Services`, `Options`, `Infrastructure`. | Spec §2; no DDD/CQRS. Controllers stay thin; all rules live in `Services`. |
| DB | EF Core 10 + SQL Server. Migrations in `Data/Migrations`. Tests use SQLite (file DB, real transactions) plus an opt-in SQL Server suite. | MonsterASP Free gives MSSQL. |
| Concurrency | Spin = one SQL transaction inside the EF execution strategy: guarded `UPDATE Customers SET SpinCredits = SpinCredits - 1 WHERE Id=@id AND IsActive=1 AND SpinCredits > 0` (row lock serialises a customer), guarded stock `UPDATE ... WHERE StockQuantity > 0`, unique `(CustomerId, RequestId)`. `rowversion` on Customer/WheelItem/Award/Settings for admin edits. | Spec §4. No double spend, no oversell. |
| Idempotency | Client sends `requestId` (UUID) per click. Replays (sequential or concurrent) return the stored attempt; unique-index violation → rollback → return stored attempt. | Spec §4. |
| Randomness | `RandomNumberGenerator.GetInt32(totalEffectiveWeight)` + cumulative ranges over basis points. | Spec §4. |
| Admin auth | ASP.NET Core Identity (`IdentityUserContext<AdminUser>`, PBKDF2, lockout) with our own cookie scheme `Admin` (cookie path `/admin`). Bootstrap only from env/user-secrets `AdminBootstrap:*`. | No public registration, no hard-coded passwords. |
| Customer auth | Phone (normalised `+201XXXXXXXXX`) + 10-char access code (31-char unambiguous alphabet, ~49 bits) hashed with `PasswordHasher<Customer>`. Per-customer lockout + IP rate limit. Cookie scheme `Customer`, session invalidated by `AccessCodeVersion` change/deactivation. | Spec §3, §8. |
| Prize images | Stored in SQL (`PrizeImages`, ≤2 MB, JPEG/PNG/WebP magic-byte check, immutable rows), served same-origin from `/media/prize/{id}`. | Survives Monster Git "Build & publish" redeploys, no filesystem write permissions needed, no path traversal surface, same-origin for canvas export. |
| Data Protection | Keys persisted to SQL (`DataProtectionKeys`). | Free plan recycles app after idle; cookies must survive restarts. |
| Front end | Razor + vanilla ES modules, GSAP 3.15.0 (bundled locally), SVG wheel in a rotating container, Web Audio–synthesised sounds (no third-party audio rights), self-hosted Outfit + Cairo variable fonts. No Bootstrap/jQuery. | Spec §2, §5. |
| Localisation | Public site Arabic RTL by default with English toggle (cookie). Admin English. Prize names AR + EN. | Spec §9. |
| Share image | 1080×1350 PNG drawn on `<canvas>` from same-origin assets; Web Share API with file → download fallback. Only prize name, tier, date and optional first name. | Spec §5D. |
| HTTPS | `Security:RequireHttps` (default true). When false (Monster Free before HTTPS ticket) the site works over HTTP but shows a "demo only — do not enter real codes" banner. | Spec §8, §13. |

## 3. Database design

- `AspNetUsers` (+ claims/logins/tokens) — `AdminUser : IdentityUser` with `DisplayName`, `CreatedAtUtc`, `LastLoginAtUtc`.
- `Customers` — `Id int`, `FullName`, `NormalizedPhone` **UNIQUE**, `InvoiceReference` (indexed), `LaptopModel`, `Notes`,
  `AccessCodeHash`, `AccessCodeVersion`, `SpinCredits` (CHECK ≥ 0), `IsActive`, `FailedLoginCount`, `LockoutEndUtc`,
  `LastLoginAtUtc`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`.
- `WheelItems` — `Id`, `NameEn`, `NameAr`, `DescriptionEn/Ar`, `Kind` (Prize/TryAgain), `DisplayOrder`, `ProbabilityBasisPoints`
  (CHECK 0..10000), `ImageId` → `PrizeImages`, `IconKey`, `ColorKey`, `StockQuantity` (nullable, CHECK ≥ 0), `LowStockThreshold`,
  `IsActive`, `IsArchived`, `CelebrationTier` (Standard/Rare/Epic), `CollectionNoteEn/Ar`, timestamps, `RowVersion`.
  Filtered unique index: only one active non-archived `TryAgain`.
- `PrizeImages` — `Id guid`, `ContentType`, `Data varbinary`, `Sha256`, `ByteLength`, `CreatedAtUtc` (immutable).
- `SpinAttempts` — `Id guid`, `CustomerId`, `WheelItemId`, `RequestId guid` (**UNIQUE with CustomerId**), `Kind` (Won/TryAgain),
  snapshot (`ItemNameEn/Ar`, `ItemKind`, `ImageId`, `IconKey`, `CelebrationTier`, `ConfiguredBasisPoints`, `EffectiveWeight`,
  `EffectiveTotalWeight`), `CreditsBefore`, `CreditsAfter`, `CreatedAtUtc` (indexed). Never updated.
- `Awards` — `Id int`, `SpinAttemptId` **UNIQUE**, `CustomerId`, prize snapshot, `Status` (Pending/Delivered), `CreatedAtUtc`,
  `DeliveredAtUtc`, `DeliveredByAdminId`, `DeliveredByAdminName`, `AdminNote`, `RowVersion`.
- `CreditLedgerEntries` — `Id long`, `CustomerId`, `Delta`, `BalanceAfter`, `ReasonCode` (Initial/AdminGrant/AdminRevoke/SpinDebit/
  TryAgainRefund), `SpinAttemptId?`, `AdminUserId?`, `AdminName?`, `Note`, `CreatedAtUtc`. Sum(Delta) == Customer.SpinCredits.
- `SiteSettings` — single strongly-typed row (Id = 1): titles, subheadings, pickup instructions, announcement, paused message
  (AR + EN), default language, sound default, spin duration min/max, animation intensity, `CampaignEnabled`, `RowVersion`.
- `AdminAuditLogs` — `Id long`, actor id/name, `Action`, `TargetType`, `TargetId`, `Details` (no secrets), `CreatedAtUtc`.
- `DataProtectionKeys` — ASP.NET Core Data Protection key ring.

## 4. Spin algorithm (`SpinService.SpinAsync`)

1. Validate `requestId` ≠ empty. Fast path: existing attempt for (customer, requestId) → return it (`Replayed = true`).
2. Campaign paused → `CampaignPaused` (no debit). Load eligible items (active, not archived, stock null/>0 for prizes);
   no eligible real prize, or configured active total ≠ 10000 → `WheelUnavailable` (no debit).
3. Execution strategy → `BEGIN TRAN`:
   1. Guarded debit `UPDATE ... SpinCredits > 0 AND IsActive = 1`. 0 rows → rollback; replay check → `NoCredits`/`Inactive`.
   2. Replay re-check under the row lock → rollback, return stored attempt.
   3. Re-load eligible items inside the tx, draw with CSPRNG. Prize with stock → guarded decrement; lost race → drop item, redraw.
      No eligible real prize left → rollback → `WheelUnavailable`.
   4. Insert attempt + ledger `SpinDebit`; Try Again → `UPDATE +1` + ledger `TryAgainRefund`; Prize → insert `Award(Pending)`.
   5. `COMMIT`. Unique violation on `(CustomerId, RequestId)` → rollback → return stored attempt.

## 5. Routes

Public: `GET /`, `GET|POST /login`, `POST /logout`, `POST /lang`, `GET /play`, `GET /my-prizes`, `GET /api/wheel/config`,
`POST /api/spins`, `GET /api/spins/{requestId}`, `GET /media/prize/{id}`, `GET /health`, `/error/{code}`.
Admin: `/admin/login`, `POST /admin/logout`, `/admin`, `/admin/customers[/create|/{id}|/{id}/edit]`,
`POST /admin/customers/{id}/credits|reset-code|toggle-active`, `/admin/prizes[/create|/{id}/edit]`, `POST /admin/prizes/allocation`,
`POST /admin/prizes/{id}/archive|restore`, `/admin/spins[/{id}]`, `/admin/awards`, `POST /admin/awards/{id}/deliver`,
`/admin/settings`, `/admin/audit`, `/admin/account/password`.

## 6. Progress checklist

- [x] Phase 0 — Inspect repo, logo, SDK; write this plan
- [x] Phase 1 — Foundation: packages, options, DbContext, entities, auth schemes, security headers, layouts, error pages
- [x] Phase 2 — Phone normaliser, access codes, admin bootstrap/login, customer login, customer CRUD, credits ledger
- [x] Phase 3 — Spin engine + odds + tests (SQLite + optional SQL Server)
- [x] Phase 4 — Customer front end: landing/login, wheel, GSAP spin, sounds, tiered reveals, Try Again, states, AR/EN
- [x] Phase 5 — Admin dashboard, prizes/allocation/effective odds, spins, awards, settings, audit; Share Your Win PNG
- [x] Phase 6 — Restore/build/test/publish, migrations + idempotent SQL script, README, `MONSTERASP_DEPLOY.md`, `TEST_CHECKLIST.md`

## 7. Verification status

| What | How | Result |
|---|---|---|
| Build | `dotnet build -c Release` | 0 warnings, 0 errors |
| .NET tests (SQLite + HTTP pipeline) | `dotnet test -c Release` | 98 passed, 27 SQL Server tests skipped (no `SPINWIN_TEST_SQLSERVER`) |
| .NET tests incl. SQL Server | same, with `SPINWIN_TEST_SQLSERVER` → local SQL Server 2022 container | 125 / 125 passed |
| Wheel maths (JS) | `node --test tests/js/wheel-math.test.mjs` | 16 / 16 passed |
| Migration | `dotnet ef database update` on local SQL Server 2022; `deploy/sql/migrate-idempotent.sql` applied **twice** to an empty DB | OK, idempotent |
| Publish | `dotnet publish -c Release`, then the published DLL run with `ASPNETCORE_ENVIRONMENT=Production` | Health, routing, auth redirects, 401 API, static files, 404 page, security headers OK |
| Browser | Chrome, desktop + 390 px mobile, AR + EN | Login, spin, Try Again, Rare reveal, Share PNG, admin dashboard/prizes/customers/awards; no console errors |

Spec §12 coverage: cases 1–11 are automated (`tests/SamyrTechWay.SpinWin.Tests`), case 12 (spin target-angle maths) is automated in
`tests/js`, and the mobile/share-PNG/audio parts are in `docs/TEST_CHECKLIST.md` for manual QA.

## 8. Known limitations / deliberate trade-offs

- **Not verified on MonsterASP itself.** Hosting, the Monster MSSQL instance, Git deploy and HTTPS activation need your account; see
  `docs/MONSTERASP_DEPLOY.md`.
- Data Protection keys are stored in SQL **unencrypted at rest** (no certificate/DPAPI available on the Free plan). Anyone with DB
  read access could forge cookies — protect the DB credentials accordingly.
- Rate limits are in-memory per app instance (fine for the single-instance Free plan; they reset on app recycle).
- Sounds are synthesised with Web Audio (no licensed audio files). Browsers only allow audio after the first user gesture.
- Admin UI is English only (customer site is Arabic + English), as planned.
- No email/SMS: access codes are handed over by staff (copy / print card) by design.
- Lockouts (customer 5 wrong codes / admin 5 wrong passwords → 15 min) can be triggered by anyone who knows a phone number or
  the admin e-mail, temporarily blocking *new* sign-ins for that account. Live sessions are not affected, and the per-IP rate limit
  slows this down. Admin recovery: `AdminBootstrap__ResetPasswordIfExists` (clears the lockout).
- Unresolved observation: in the automated Chrome session used for QA, the admin session twice failed to survive a local app
  restart. Scripted HTTP clients holding the same cookies (admin + customer + antiforgery) kept the session across repeated
  restarts, and the automated tests pass, so the cause looks specific to that browser automation. It is on the manual
  checklist to confirm in a normal browser.
- Customer login timing: a known phone with a wrong code costs one or two extra local DB round trips (~1 ms) next to ~100 ms of
  PBKDF2, so a patient attacker could statistically tell registered phones apart. The message is identical, a phone alone grants
  nothing, and attempts are rate-limited.

# CLAUDE CODE MASTER BUILD PROMPT — SAMYR TECH WAY SPIN & WIN

> This document is the authoritative full product specification and execution brief. Implement a complete, running application in this repository. Read the entire document before changing code. Follow the project instructions in `CLAUDE.md` too.

## 0. YOUR MISSION, ROLE AND QUALITY BAR

You are acting as a staff-level ASP.NET Core architect, experienced full-stack engineer, game-interaction designer, accessibility specialist, database engineer, QA engineer and deployment engineer. Build **SAMYR TECH WAY — SPIN & WIN**, a premium, fully functional, branded promotional prize-wheel website for an Egyptian laptop seller, including a secure customer experience and private admin dashboard.

The business concept is SIMPLE, the experience must be EXCEPTIONAL. Deliver a production-minded MVP, not a design-only mockup, not pseudocode, and not a generic admin template. Every visible control must work and persist its intended actions. Build real application code, EF Core entities and migrations, forms, APIs, responsive views, animation systems, validation, meaningful tests and deployment documentation.

**Priority order**:
1. Correct eligibility/credits/prize outcomes and no double-spend.
2. Security and private customer information.
3. A polished, world-class responsive experience faithful to the uploaded logo.
4. Fully functioning admin tools.
5. Easy hosting on **MonsterASP.NET Free**, including GitHub-based deployment from macOS/VS Code.
6. Maintainable simplicity. No overengineering.

**Execution instructions**:
- First inspect the existing solution, assets, SDK version and repository. Preserve useful files. If a skeleton MVC project exists, extend it instead of recreating everything.
- Write an actionable brief implementation plan and database design to `docs/IMPLEMENTATION_PLAN.md`, then implement instead of stopping at the plan. Do not ask routine decisions; choose documented safe defaults. Ask only when essential secrets or external account actions require user input.
- Work incrementally and perform `dotnet restore`, `dotnet build`, tests and a Release publish when the local environment supports them. Fix ALL compile errors and failures rather than leaving them to the user.
- When tooling/network/SQL connection is absent, still implement thoroughly and report specifically what could not be verified. Do not claim unexecuted tests passed.
- Keep a short progress checklist in `docs/IMPLEMENTATION_PLAN.md` and a useful `README.md` with local setup and deployment instructions.
- **Do NOT stop after designing the UI, creating static pages or generating placeholder controllers.** Continue until the functional MVP is implemented and tested within the capabilities available.

## 1. BRAND IDENTITY — USE THE REAL PROVIDED ASSET

Brand name: **SAMYR TECH WAY**. Secondary text on original logo: **EL-MOSTAWRYD — Laptop Trading**.

Use this *exact* user-provided PNG file as the canonical asset:
`src/SamyrTechWay.SpinWin/wwwroot/assets/brand/samyr-tech-way-logo.png`

- It is a transparent PNG featuring a phoenix/bird emblem, glossy ruby/red/orange/amber/gold gradients, circular mark, laptop glyph and distinctive typography.
- **Do not recreate, replace, rename, redesign, AI-generate, distort, recolor or substitute the logo with a generic phoenix.** Do not add a randomly sourced logo.
- Make the original full logo visibly readable; parts of its smaller typography are dark, so use a designed suitable backing panel or contrast treatment when displaying the full logo on a dark background. Use the phoenix motif subtly as a visual *inspiration*, not as an unauthorized replacement for the logo. Full logo on desktop entry/landing; elegantly scaled header mark on mobile.
- Suggested UI palette (adjust based on inspecting actual logo pixels): near-black `#09090D`, charcoal `#171116`, deep ruby `#A80710`, flame red `#E51B16`, fiery orange `#FF7518`, metallic amber `#FFB62D`, pale champagne `#FFE08A`, near-white `#FFF7ED`.
- Background predominantly luxurious black/charcoal with very controlled gold and ember gradients. NOT blue/purple neon. NO childish casino theme, rainbow palette, overdecorated UI, emoji-as-icons, random stock photos or generic bootstrap look.
- Art direction: luxury laptop/technology campaign + premium interactive game; 2026-level motion and craftsmanship. Typography: e.g. Outfit/Manrope for English and Cairo/Tajawal for Arabic with graceful fallbacks; minimize external font requests if possible.
- Use elegant rounded corners, crisp high contrast labels, subtle brushed-metal/glass highlights, deliberate spacing, tiny glowing particle accents, ambient gradient halos and golden reflections. Make this sophisticated, not noisy.
- Responsive first: excellent iPhone widths (~320–430px), mid-size tablets and full desktop; avoid horizontal scroll.
- Have a thoughtful empty state, offline/error state, loading/saving state, celebration state, already-played state, out-of-credits state and out-of-stock state.
- Add reduced-motion mode, accessible focus styles, keyboard access and adequate contrast.

## 2. EXACT TECHNOLOGY AND HOSTING REQUIREMENTS

- **ASP.NET Core 10 MVC**, Razor Views, standard server-rendered pages, minimal JSON endpoints only where necessary for interactive spin/CRUD interactions.
- C# / .NET 10; EF Core 10 SQL Server provider; MSSQL is the primary database **from the start**, because MonsterASP.NET Free includes a hosted MSSQL database.
- One web project plus a small test project. Aim for low memory use on hosting capped at 256 MB RAM. Avoid multiple hosted processes.
- HTML5, semantic CSS3, vanilla modern JavaScript modules; GSAP for rich motion, Canvas/CSS/SVG for wheel visualization as justified; optional local libraries for the image export. No React, Vue, Angular, Next.js, Node.js SSR, Docker, Redis, background worker infrastructure, WebSockets, Hangfire, Puppeteer, Playwright on the server, FFmpeg, paid API, or queue service.
- Any frontend dependency must be lightweight, version-pinned and bundled with the site where practical (no runtime npm requirement on hosting). Prefer inline SVG/icons bundled locally or Lucide SVG sprite.
- Proper EF Core migrations; nullable reference types, structured application logging, async I/O, DI and MVC validation.
- Store timestamps in UTC, convert to Africa/Cairo for admin reporting.
- Avoid complex DDD/CQRS/microservices/tenancy patterns. Use a sensible clean MVC folder organization: `Controllers`, `Areas/Admin/Controllers`, `Views`, `Areas/Admin/Views`, `Models/Entities`, `Models/ViewModels`, `Data`, `Services`, `Options`, `wwwroot/css`, `wwwroot/js`, `wwwroot/assets` and tests.
- The final build MUST work on **Windows/IIS behind MonsterASP.NET Free**. Do not rely on shell access to the host, on a permanently running process, or on a Mac-only framework/runtime file.

## 3. PRODUCT BEHAVIOR — CORE USER JOURNEY

1. Admin logs into `/admin/login` (no public admin registration).
2. Admin creates a customer after the customer purchases a laptop, entering full name, mobile phone and optionally invoice/reference, purchased laptop model and internal notes.
3. Customer receives **one Spin Credit** by default, plus a secure unique **access code** generated server-side; admin can copy/print it at creation time. There is NO SMS OTP dependency in v1; typing only a phone number must NEVER allow anyone to enter that customer's account.
4. Customer visits the branded website, enters the normalized phone number and private access code, then signs in. Customer sees their first name, currently available credits, the giant premium wheel and a single clear CTA.
5. When they press **SPIN**, a single server-side transactional operation validates availability, atomically consumes one credit, securely samples the actual weighted result, and commits it to the DB. The frontend receives a saved attempt/result and **only then** plays the spin animation to the correct wheel segment.
6. Normal prize: show matching premium celebration, prize image/name and precise pickup instructions. Deduct that credit permanently, create a pending award/claim record, update stock if tracked, and show result in admin.
7. **TRY AGAIN**: this is a configurable, visible special wheel item. It is NOT a physical gift. The user receives a friendly suspense/retry reveal, and the system returns the spent credit **within the SAME transaction**. The customer can immediately spin again without admin permission. Log every Try Again attempt. They can hit Try Again several times in a row until they eventually win a real prize; guard against a configuration that has no eligible real prize.
8. When credits reach zero, hide/disable Spin and show a warm branded 'You've already played / your prize' state. Refreshing, closing tabs, switching devices, double clicking or manipulating the JS must never create free credits or a second result.
9. Admin may manually grant another credit to a specific customer at any time, with required reason and audit trail. This allows a new spin even if customer won previously. Preserve old attempts and awards.
10. Customer can view *their own* win history (including final real prizes) and share a visually beautiful branded **Share Your Win** PNG image or system share sheet, with NO phone, access code, invoice, claim secret or private information.

## 4. CRITICAL BUSINESS RULES AND EDGE CASES

- Spin Credits are an integer >= 0. Granting credits is admin-only. Customer cannot mutate balances via requests except through the server-controlled valid Try Again refund.
- EACH click spins exactly once. Implement request-level **idempotency** using a UUID/unique operation key, a unique DB constraint scoped to the customer, and return the existing attempt if the identical key is replayed. Do not decrement or refund twice.
- The spin transaction must be safe against concurrent requests from separate tabs/devices. Use a proper SQL Server transaction, row-version/concurrency strategy or guarded atomic SQL update (`UPDATE ... WHERE SpinCredits > 0`), proper unique constraints, stock consistency and a retry/clean error strategy. Prevent overselling the last available prize under concurrent winners.
- Persist the actual result BEFORE animation; do not draw the prize client-side or trust the client's target result. A wheel refresh should show the completed/stored result, not grant a new spin. If result persistence succeeds but network reply fails, reusing the same idempotency key must safely return that exact result.
- Every attempt is an immutable audit entry including attempt ID, customer ID, chosen wheel item, type, credits before and after, UTC created timestamp, idempotency key, and a snapshot of the item's name/media/weight as required for reliable history. A real prize gets a separately tracked award status. Retry remains a visible attempt, but not a fulfilled award.
- Stock is an optional per-real-prize integer. `null` means unlimited stock. On winning, atomically reserve/decrement stock, not only when the gift is physically handed out. Admin can subsequently mark award as delivered/claimed and see pending/delivered status. Never delete an item already referenced by attempts: use deactivate/archive (soft delete).
- If a prize is inactive or stock zero, it cannot win. Derive **effective** probabilities by renormalizing active, in-stock eligible item weights. Make admin preview show configured weights AND currently effective draw odds. A Try Again item is only eligible while at least one real prize is eligible. If no real prize is eligible, **block all spins BEFORE consuming credits** and display an admin warning plus customer-friendly maintenance state.
- Use percentage values represented with decimal-friendly integer basis points (1%=100 points, total active configured weight 10,000) to avoid floating-point errors. Draw with `RandomNumberGenerator.GetInt32(totalEffectiveWeight)` and a cumulative-range selection. Do not use `Math.random` or `new Random` for server prize decisions.
- Configurable wheel items can include real prize and retry. Allow exactly one active `TRY_AGAIN` entry initially (admin can edit its name/icon/image/weight). Prize items can use image upload OR selected built-in safe icon. Sorting/order is admin configurable.
- On editing probability allocations require configured active weights to sum to 100.00%, with clear validation and live total, plus effective odds preview after stock filtering. Reject invalid saves and disable spins if configuration invalid. Do not pretend equal-sized display wedges mean equal odds: use **probability-proportional wedge spans** wherever practicable and show an accessible legend for tiny wedges. Pointer/wheel stop must exactly match the server-selected wedge and should not land on separator edges.
- Do not retroactively modify previously awarded prizes when their probability/name/image changes. Previously persisted prize result remains canonical.
- Login codes: generate cryptographically secure, sufficiently long one-time-displayed customer access codes (prefer 8–10 chars in an unambiguous alphabet), store only strong salted hashes; allow admin to reset and display a NEW code; invalidation of old code on reset. Never store plaintext in customer DB. Use secure cookie authentication with separate admin/customer identities and explicit authorization policies.
- Normalize phone inputs for Egypt: recognize `01XXXXXXXXX`, `+201XXXXXXXXX` and `201XXXXXXXXX` without unsafe ambiguity; store normalized international format; enforce unique normalized phone number per customer. Allow basic country-code extension only if cleanly implemented. Provide reasonable UX formatting and validation.
- Admin ability to adjust credits and customer activation state must persist, require audit, and not silently delete records. Editing credits cannot produce negative balance.
- Show errors gracefully, with no sensitive stack traces in Production. Never expose customer list/phone numbers outside admin, even in API responses or downloadable share graphics.

## 5. CUSTOMER PAGES AND UX — PREMIUM VISUAL EXECUTION

### A. Public landing / login (`/` and `/login`)
- Branded immersive hero: original SAMYR TECH WAY logo as provided, an elegant short headline 'YOUR PURCHASE. YOUR MOMENT. YOUR REWARD.' and meaningful Arabic-language variant.
- Introduce the promotion in 1–2 short lines: a laptop purchase unlocks one chance to win a branded gift.
- Stylish phone field with `+20` support, access-code field with visibility toggle, excellent mobile keyboard types, human error states, loading, rate-limit-friendly errors.
- No public signup. The admin exclusively registers buyers.
- Rich dark typography with flame-gold gradients, purposeful entrance motion and small phoenix-inspired ornament details.
- Brief real promotion terms and privacy notice, without presenting gambling as a cash lottery.

### B. Main wheel (`/play`)
- Visually dramatic, extremely well-crafted *central wheel* inspired by premium casual mobile gaming, NOT a roulette casino.
- Hero area with compact brand, friendly customer greeting, available Spin Credits and subtle glowing indicator.
- Big, true circular wheel; richly shaded segments using the actual current wheel items and photos/icons; responsive pointer at 12 o'clock and dynamic center `SPIN` CTA.
- Beautiful radial gradients, metallic gold outer ring, glowing LED ticks, soft fire/red particles, subtle depth, realistic highlights, rotating ring accent. Strong legibility on dark backgrounds.
- Show prize legend/details for small segments and optionally hover/tap to inspect an item.
- Use GSAP timeline for confident acceleration, multiple real rotations, premium deceleration (e.g. 5–7 seconds; initial speed and total duration configurable). Accurate angle math must guarantee end stop inside server-chosen segment with a safe angular margin. A graceful reduced-motion version jumps quickly to result while remaining correct.
- **Audio**: user-gesture-initiated soft start/whoosh, discrete tick while crossing wedges, tension/riser near stop, delightful win chime; for Try Again a friendly playful sound; use Web Audio API and/or bundled, rights-cleared compressed audio. Global mute/sound toggle, remember preference, set respectful volume. Respect autoplay restrictions—do not play sound on page load.
- **Haptics** (if available): gentle optional vibration on mobile, not required and must not break unsupported browsers.
- During spin: prevent accidental second action, guard transitions and expose loading state. If the server returns a previously committed idempotent result, do not create a second attempt.
- Floating motes/confetti should be performant; cap particles and frame budget on low-powered phones.
- First impression should be 'WOW', but UX must stay usable and load quickly on the free plan.

### C. Result / Try Again reveal
- Common state: focused outcome reveal synchronized with wheel stop, no unexplained delay, accessible text output.
- **Normal gift**: alternate celebration tiers settable per prize (`Standard`, `Rare`, `Epic`). Standard = subtle glow/confetti; Rare = more gold particles, spotlight, celebratory chimes; Epic = phoenix-like fire/gold burst, spotlight beams and a memorable cinematic reveal. All three share a cohesive brand language; CSS/Canvas/GSAP, not giant videos.
- Large actual prize image/icon, accurate name, thank-you copy, 'Collect from our store / contact sales for collection' configurable message, a compact history card and action 'Share Your Win'.
- **Try Again**: show special orange/gold 'SO CLOSE! ONE MORE SPIN' moment with playful small bounce animation and `SPIN AGAIN` CTA. Do not falsely claim they won a physical gift. Credit stays available through a transactional refund. On each retry create a fresh idempotency key when user explicitly clicks again. Show clear language that no prize has been awarded yet.
- **Out of credits/already played**: beautiful tailored state, show their most recent actual award if one exists, and friendly messaging that only the shop/admin can grant another spin.

### D. Win sharing
- `Share Your Win` should create a **beautiful, branded 1080×1350 or 1080×1080 PNG** locally in the customer browser, with the authentic logo, flame/amber gradients, prize image/icon, actual prize name, 'I WON!' and optional first name (privacy-friendly; default is first name only or omit). NO private identifiers, reward codes, phone, receipt, invoice, QR token or secrets.
- Use Canvas (or reliable, small client-side library) with local same-origin images and fonts; handle image failures and CORS; support one-click download, Web Share API when supported, and desktop copy/share fallback. Use an accurate preview dialog before sharing. The export must be real and functional, not just a button.
- The image must reflect the actual persisted win and be available again from customer history; NEVER fabricate a win in the frontend.

## 6. ADMIN DASHBOARD — FULLY WORKING, BEAUTIFUL, SIMPLE

Location `/admin`, secure separate admin login, sidebar/topbar, responsive table-to-card layouts, cohesive brand palette but more restrained and data-focused than customer wheel. Protect every admin controller/action at the server, not only links in UI.

### A. Dashboard home `/admin`
- KPI cards for customers, available/consumed credits, total attempts, retries, real wins, unclaimed/claimed awards, active prizes; accurate SQL queries.
- Small recent attempts table with date/time (Egypt time), phone partially masked or full visible only in admin, item, result type and link to customer.
- Recent awards needing delivery and stock alerts.
- Optional tiny seven-day activity chart implemented without an enormous charting dependency.

### B. Customers `/admin/customers`
- Search by customer name, normalized phone and invoice; filters by active/disabled and credits > 0; pagination.
- Add customer: name (required), phone (required and unique), invoice (optional), laptop model (optional), notes (optional), credit grant default 1 (modifiable by admin). Generate login access code securely and reveal one time with Copy action and printable mini receipt/card. Do not re-display stored plaintext afterward.
- Edit customer details, activate/deactivate, reset access code (one-time reveal), grant additional credit(s) with required reason, view credit transactions, view full attempts and awards.
- Visual statuses: eligible, finished, disabled. Never mislabel an active customer with extra credits as finished.
- Friendly duplicate phone error, not generic server exception.

### C. Wheel items / prizes `/admin/prizes`
- CRUD for names (Arabic and English if localization is implemented), short description, item type (`PhysicalPrize` or `TryAgain`), JPEG/PNG/WebP image upload OR safe built-in icon, wheel display sort order, weight percentage, stock nullable (for physical), active toggle, reveal tier, optional collecting note.
- Drag-sort is nice but not essential; numeric order field is adequate. Image previews, image validation and safe randomized filenames; max image size ~2MB and safe accepted signatures. Never allow HTML/SVG/scripts/executable uploads, arbitrary remote URLs or unsafe paths.
- Persistent stock display and low-stock alerts, and no hard-delete of referenced records.
- Live admin probability total; enforce active configured weights = 100.00%. Preview a sample wheel configuration and current *effective* odds after out-of-stock items are excluded.
- Try Again has configurable weight and title/icon; only one special retry item active; it never generates an award and has no inventory.

### D. Spin attempts `/admin/spins`
- Timestamp, customer, invoice, item, result kind (`Won`/`TryAgain`), credits before/after, reference ID; date filters, search, pagination.
- Attempt details and authoritative history; do not let admin edit past outcome.

### E. Awards `/admin/awards`
- Pending/delivered filter, link to source spin and customer, item snapshot, created/delivered timestamps, notes and admin who delivered.
- Simple `Mark as Delivered` protected action and audit trail; prevent marking twice incorrectly and handle concurrent updates.
- Do not implement complex inventory/POS/QR scanning unless already in scope; we want a small application.

### F. Settings `/admin/settings`
- Global site title and promo subheading, default post-win pickup instructions, language preference if supported, sound default, spin duration range, animation intensity, announcement copy, optional campaign enabled/paused flag.
- Site pause must stop new spins and keep existing account/history accessible. Use reasonable safe defaults and validation; ensure branded logo cannot accidentally be removed via generic settings.
- Audit admin settings and credit changes.

## 7. DATA MODEL SUGGESTION (REFINE WITHOUT OVERCOMPLICATING)

At least:

- `AdminUser` / ASP.NET Core Identity for admin password hashing, authorization and lockout; default admin created only when credentials are provided via user secrets/environment configuration; no public register route and no hard-coded passwords.
- `Customer`: `Id`, `FullName`, `NormalizedPhone` UNIQUE, optional `InvoiceReference`, `LaptopModel`, `Notes`, `AccessCodeHash`, `SpinCredits` >= 0, `IsActive`, UTC audit timestamps, optimistic concurrency token (`rowversion`).
- `WheelItem`: `Id`, name(s), description, `Kind` (`Prize`, `TryAgain`), `DisplayOrder`, `ProbabilityBasisPoints`, `ImagePath` or `IconKey`, `StockQuantity` nullable, `IsActive`, `CelebrationTier`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion`, soft-delete/archived status.
- `SpinAttempt`: `Id` Guid, `CustomerId`, `WheelItemId`, `RequestId` GUID/unique with customer, item/odds snapshots, `Kind`, `CreditsBefore`, `CreditsAfter`, `CreatedAtUtc`, immutable.
- `Award`: `Id`, `SpinAttemptId` UNIQUE, `CustomerId`, immutable prize snapshot, `Status` (`Pending`, `Delivered`), created/delivered dates, `DeliveredByAdminId`, optional private admin note.
- `CreditLedgerEntry`: `Id`, `CustomerId`, `Delta`, `ReasonCode` (`Initial`, `AdminGrant`, `SpinDebit`, `TryAgainRefund`), `SpinAttemptId` optional, `AdminId` optional, timestamp, human-readable reason. Must reconcile with customer's stored credits.
- `AppSetting` (key/value or strongly modeled row) and `AdminAuditLog` (actor/action/target/timestamp/metadata without secrets).

Use integer primary keys or Guids consistently, SQL indexes for phone, operation id, created timestamps, customer foreign keys, and stable constraints for idempotency and positive counts. Configure decimal display of percentages from basis points in view models. Seed **sample prizes ONLY with explicit demo/development mode**; never seed demo customers/awards into live production. Example gift catalog for local testing: Mouse Pad, Cleaning Kit, Laptop Stand, Wireless Mouse, Laptop Backpack, Gaming Headset and Try Again. These examples must remain fully editable from admin.

## 8. SECURITY AND PRIVACY

- Secure production HTTPS is mandatory before handling real phone numbers or access codes. On MonsterASP.NET Free, user must request activation from support after uploading the site; document this clearly. Before HTTPS activation, run only a non-sensitive placeholder/demo and do NOT encourage live logins on plain HTTP.
- Use secure, HTTP-only cookies, SameSite and sensible expiration; correct forwarded/IIS HTTPS scheme handling. Separate customer/auth/admin cookies and explicit policies.
- ASP.NET Core Identity with secure password hashing for admin; customer code via approved secure hasher; crypto-secure access code generation. Never send hashes to client. No client-side-only admin authentication.
- CSRF antiforgery on all state-changing cookie-authenticated POST requests, including AJAX. Validate inputs server-side, protect forms, avoid reflected XSS, use Razor HTML encoding; add reasonable CSP/security headers as compatible with CSS/JS behavior.
- Rate-limit login and spin endpoints by IP/account, avoid exposing whether a customer's phone exists, log abuse but not secrets; lockouts for repeated customer code failures and admin passwords.
- File upload hardening (size/extensions/signature/randomized names, no SVG, no uploads outside designated public images folder); do not execute user content.
- Do not store private credentials, real customer phone numbers or hosted database passwords in Git, documentation, logs, screenshots or static JS. Use `dotnet user-secrets` locally and MonsterASP control panel environment variables in production. `.gitignore` includes `appsettings.*.local.json`, files with secrets, bin/obj and build output.
- Data Protection/cookie encryption key persistence should be verified for IIS hosting/restarts and safely configured if needed; avoid memory-only keys that invalidate sessions after every idle restart.
- Do not expose raw stack traces when running in Production. Use friendly 403/404/500 pages consistent with brand.
- No SMS, email provider, payments, shipping, POS integration, marketing tracking, third-party analytics or unnecessary customer PII in this MVP.

## 9. ENGINEERING + UX QUALITY REQUIREMENTS

- Everything should feel unified across loading/error/success states. Avoid template ugliness or inconsistent components.
- Use real SVG icons not emojis; authentic uploaded logo; original CSS illustration and shapework, not generic internet image assets. Make missing prize image fall back to a visually tasteful symbolic prize tile.
- Consistent responsive admin design, no unstyled default browser inputs, real notification/toast system, confirmation for destructive operations and sensible skeleton/loading states.
- All forms should keep user input and show accessible inline error messages. Avoid naive full-page reload on the spinning experience (Razor full page reloads OK for admin).
- Respect user reduced motion and sound toggle, pause animation when tab hidden appropriately while preserving result, never replay or grant spins just because a page loads.
- Inform user how many credits remain; don't promise everyone wins a physical prize until after a non-retry result. No fabricated user counts or fake winner ticker.
- Keep image optimization sensible and avoid huge files. Original full logo can be used as provided; additional thumbnails may be generated as derivatives locally without altering the logo's actual artwork.
- English CTA/headline styling can coexist with Egyptian Arabic body copy. Prefer Arabic RTL as public default and an English LTR toggle if feasible without harming launch speed; at minimum all messages must be coherent Arabic or English, with proper RTL layout for Arabic. Admin can be English-first if that makes the result cleaner.
- Build for mobile Safari and Chrome, not only desktop Chrome.

## 10. REQUIRED ROUTES / CONTRACTS (REFINE AS NEEDED)

Public:
- `GET /` branded entry.
- `GET /login`, `POST /login` customer code sign-in.
- `POST /logout` authenticated sign-out.
- `GET /play` guarded wheel.
- `GET /api/wheel/config` authenticated items with safe presentation metadata, never login codes or customer data.
- `POST /api/spins` anti-CSRF, authenticated, idempotency key required; authoritative result ID/item ID/valid target + updated credit count; well-defined 200/201/400/401/403/409/429 responses.
- `GET /my-prizes` customer authenticated win history; safe metadata for Share Your Win.

Admin:
- `/admin/login`, `/admin/logout`, `/admin` with separate cookie/auth scheme/policy.
- `/admin/customers`, `/admin/customers/create`, `/admin/customers/{id}`, `.../grant-credit`, `.../reset-code` etc.
- `/admin/prizes`, `/admin/spins`, `/admin/awards`, `/admin/settings`.

All state mutations need server-side permission checks and audit. No public customer self-registration.

## 11. PHASED IMPLEMENTATION — ACTUALLY DO THE WORK

Complete the phases in this order, recording progress without asking for routine sign-off:

**Phase 1 — Foundation.** Inspect repo and logo. Create/fix ASP.NET Core 10 MVC solution and a small test project, organize assets, configure EF Core SQL Server (via environment/user secrets) and DI, add admin/customer authentication and layout shells. Write initial plan and DB migrations.

**Phase 2 — Database and authentication.** Implement normalized customer identity, one-time-displayed hashed access codes, admin login and secure bootstrap, customer CRUD and credits ledger. Establish safe initial login flow.

**Phase 3 — Spin engine.** Implement and unit/integration test the transactional and idempotent weighted spin engine with Try Again refund, real prize award, stock reservation, invalid-config/empty-stock handling, admin credit grants and immutable attempt logs.

**Phase 4 — Premium customer front end.** Polish exact brand identity, login, luxury wheel, animation stop math, sounds, tiered prize celebrations, retry reveal, history/credits state, mobile accessibility, performant ambient effect layers.

**Phase 5 — Dashboard + share.** Build complete dashboard and working management forms, effective odds preview, award delivery, settings and audit. Implement real client-side branded share PNG generation and preview with privacy checks.

**Phase 6 — Verification and deployment.** Build/restore/test/publish. Add test cases for key edge conditions; prepare `docs/MONSTERASP_DEPLOY.md`, full README, production config checklist, initial admin creation instructions and optionally a release smoke-test list. Fix any failures you can reproduce. Output the actual commands to run and the exact browser routes.

Do not hide failures with empty catch blocks, todo placeholders or dummy static numbers. If you must defer a noncritical polish item, note it transparently in `README.md` while core flows remain functional.

## 12. MINIMUM MANDATORY TESTS

Add automated test coverage especially for:
1. Phone normalization Egyptian 01 / +20 / 20 variants and reject malformed inputs.
2. Active customer gets one initial credit, cannot spin with zero or when disabled.
3. A real win decrements credit once, reserves stock once, creates ONE award and ONE attempt.
4. Try Again deduct+refund happens atomically, keeps credits unchanged, logs attempt, creates NO award, user may retry with fresh operation key.
5. Multiple consecutive Try Again results remain possible until a real prize; never dead-end if eligible prizes exist.
6. Duplicate idempotency key returns exactly same saved result without another debit/award/refund.
7. Parallel attempts from separate devices/tabs cannot overdraw credits or oversell one item of stock. Ensure at least one SQL Server-backed concurrency test or provide honest manual integration test commands if a live DB is not available.
8. With zero eligible real prizes, block spin WITHOUT debiting.
9. Invalid probability allocation cannot be activated; inactive/sold-out prizes excluded and effective odds reflect that.
10. User cannot reach another user's attempts/prizes, anonymous user cannot use the API, and customer cannot access `/admin`.
11. Admin grant adds auditable credits; redemption/delivery is idempotent.
12. Mobile/share PNG behavior and spin target-angle math tested with representative segment configurations or included in manual QA checklist.

## 13. MONSTERASP.NET FREE DEPLOYMENT ACCEPTANCE

- One compiled ASP.NET Core application deployable on Windows IIS, **prefer framework-dependent .NET 10**, standard `dotnet publish -c Release` output with `web.config` generated automatically.
- GitHub -> MonsterASP control panel -> Git deploy -> 'Build .NET app' -> select correct project/branch -> manual **Pull latest** and **Build & publish**. On Free, automatic deploy-on-push is NOT included; do not claim it is.
- Production DB hosted on MonsterASP MSSQL Free. Configure `ConnectionStrings__DefaultConnection` (or consistent named key) in MonsterASP **environment variables**, not committed `appsettings.json` secrets. Use the **local/internal** MSSQL connection string for the app hosted with MonsterASP. For macOS development and local EF CLI, use the remote/public MSSQL connection string with remote access explicitly enabled, or a local developer SQL Server alternative; store it in .NET user-secrets (never in Git).
- No assumption that SQL Server, SQL Server LocalDB, or SSMS is natively installed on an Apple Silicon Mac.
- Clearly document migrations: create migration locally with `dotnet ef migrations add`, apply with `dotnet ef database update` through temporary remote access or a carefully reviewed idempotent SQL script run in Monster's WebMSSQL / Run T-SQL UI. **Back up before production migrations and do not blindly drop a live database.** Disable remote access when not needed.
- MonsterASP Free provides a free host subdomain and supports current .NET. Free limits include ~256MB website memory, 1GB SQL database, capped resources/traffic, no automatic daily backups and no AlwaysRunning. 3-hour idle shutdown can cause slow first request; site and DB archived after ~6 months of disuse. Avoid in-memory-only critical state or background timers; persist everything in SQL.
- **HTTPS on Free requires uploading first then opening a support ticket for activation.** Until active, do not enter real customer credentials over HTTP. After enabling HTTPS, force redirect and secure cookies.
- Support alternative folder publish + SFTP/FTPS to `/wwwroot` (the hosting document root), but Git deploy is the recommended Mac/VS Code path. Clarify that project `wwwroot` (static assets) sits INSIDE published webapp content under host `/wwwroot/wwwroot` and is NORMAL. Never upload source files straight to host `/wwwroot` as though they were a compiled app.
- Host supports IIS InProcess. No reliance on Node runtime/npm/Python/Docker/SSH on host.
- Supply post-deploy health checks: login, create buyer, copy access code, test winning result, retry path, refresh after committed result, admin sees same result, credit recharge, award delivered, mobile, icon/uploads, HTTPS, share image, restarts/idempotency.

## 14. DELIVERABLES AND FINAL RESPONSE

When complete, produce in the repository:
- Working ASP.NET Core MVC application and tests.
- EF Core migrations and repeatable explicit admin bootstrap.
- Original logo actually displayed properly, plus full customer and admin UI.
- Sound/animation and Try Again workflow, real share image export.
- `README.md` for macOS VS Code quickstart and commands.
- `docs/IMPLEMENTATION_PLAN.md`, `docs/MONSTERASP_DEPLOY.md`, and `docs/TEST_CHECKLIST.md`.
- No real secrets or personally identifiable demo data in source.

In your final message, give a succinct status of what was actually implemented, exact commands executed + their results, exact local URL and admin route, how to create admin login securely, migrations and deploy checklist, and honestly list any unverified external steps. Do not falsely claim that hosting or HTTPS are configured until the user has performed those account actions.

**Begin immediately: inspect the repository and logo; write the plan; implement the full application end-to-end; validate; report.**

# Test checklist

## Automated (run before every deploy)

```bash
dotnet test -c Release                                   # SQLite + in-process HTTP pipeline; SQL Server suite shows as skipped
SPINWIN_TEST_SQLSERVER="<server, no Database=>" dotnet test -c Release   # runs the SQL Server suite too (creates/drops its own DB)
node --test tests/js/wheel-math.test.mjs
```

| Spec §12 case | Where |
|---|---|
| 1 Egyptian phone normalisation | `Unit/PhoneNumberTests` |
| 2 Initial credit; zero credits / disabled can't spin | `Engine/SpinEngineTests` (+ SQL Server copy) |
| 3 Win: one debit, one stock reservation, one award, one attempt | `Engine/SpinEngineTests` |
| 4 Try Again refunds atomically, logs attempt, no award | `Engine/SpinEngineTests` |
| 5 Consecutive Try Agains never dead-end | `Engine/SpinEngineTests` |
| 6 Idempotency key replays the same result | `Engine/SpinEngineTests`, `Web/WebTests` (201 then 200) |
| 7 Parallel spins can't overdraw credits or oversell stock | `Engine/ConcurrencyTests` (**SQL Server** row locks) |
| 8 No eligible prize → blocked without debit | `Engine/SpinEngineTests` |
| 9 Invalid allocation can't be saved; sold-out/inactive excluded | `Engine/AdminRulesTests`, `Unit/WheelOddsTests` |
| 10 Cross-customer isolation, anonymous API, customer → `/admin` | `Web/WebTests` |
| 11 Admin credit grants audited; delivery idempotent | `Engine/SpinEngineTests` |
| 12 Wheel target-angle maths | `tests/js/wheel-math.test.mjs`; share PNG and mobile below |

## Manual QA (local, before go-live)

Use Chrome and Safari on desktop, plus a real phone (or DevTools at 390×844), in **Arabic and English**.

### Customer
- [ ] Landing shows the original logo, sharp and not stretched. Arabic is right-to-left; the language toggle switches to English and is remembered.
- [ ] Phone formats `01012345678`, `+20 101 234 5678` and Arabic-Indic digits all sign in; lower-case codes and codes with a dash work.
- [ ] A wrong code and an unknown phone show the **same** message. After 5 wrong codes the account is locked for 15 minutes.
- [ ] `/play`: credits show; every slice is the same size and no percentages appear anywhere (odds are server-only); prize photos and icons load; labels are upright on both halves.
- [ ] Spin: the button is disabled while spinning, the pointer lands **inside** the winning slice, ticks and sounds play, and the
  sound toggle mutes everything (and is remembered).
- [ ] **Try Again**: the reveal says the credit is back, credits are unchanged, and spinning again works.
- [ ] **Standard / Rare / Epic** wins each show their own reveal (Epic is the biggest). The pickup note shows. Credits go down by one.
- [ ] Reload the page right after a win: the same latest award shows and no extra credit is used.
- [ ] Double-clicking Spin, or pressing it in two tabs at once, never uses more credits than you have.
- [ ] Zero credits: the button is disabled with an explanation. A paused campaign shows the paused message.
- [ ] Network off during a spin: a friendly retry message shows. Retrying returns the **same** result (no second debit).
- [ ] **Share Your Win**: a 1080×1350 PNG with logo, prize, tier and date (plus the first name only if the customer ticks that option). The phone share
  sheet offers apps, and desktop downloads the file. The image contains no phone number and no access code.
- [ ] `/my-prizes` lists only this customer's prizes and their status.
- [ ] OS "Reduce motion" on: the wheel still lands correctly, with gentler effects and no confetti storm.
- [ ] Keyboard only: you can tab to the login, Spin, reveal and close buttons, and focus is visible. A screen reader announces the result.

### Admin
- [ ] `/admin` while signed out → admin login. A customer session can't open any `/admin` page.
- [ ] Dashboard numbers match the database (customers, credits outstanding, spins, wins, pending awards, low stock). The chart has a table view.
- [ ] Create a customer: the code shows **once**, and copy/print works. A duplicate phone (any format) gives a friendly error.
- [ ] Grant +3 / revoke −1 credits with a reason: the ledger and audit log update, and credits can't go below zero.
- [ ] **Set spins to 0**: credits drop to 0 with a ledger entry, and the customer's wheel shows "no spins left".
- [ ] Reset code: the old code fails, the customer's open session is signed out, and the new code works.
- [ ] Disable a customer: their session ends and they can't sign in.
- [ ] **Delete a customer**: refused while they have a prize awaiting delivery; otherwise the customer, spins, awards and ledger are gone, their session ends, the phone can be registered again, and the audit log keeps a "حذف عميل" row.
- [ ] Prizes: editing one prize's weight on its edit page saves, and Try Again changes by the difference (shown in the message).
- [ ] Prizes: allocation ≠ 100 % can't be saved. Effective odds update when stock runs out. Upload a JPEG/PNG/WebP ≤ 2 MB; a fake
  `.png` (e.g. a renamed text file) is rejected. Archiving needs the item to be inactive first.
- [ ] Two admins editing the same customer or prize: the second save gets a "changed by someone else" message.
- [ ] Awards: mark delivered. Clicking it twice is harmless.
- [ ] Settings: text changes appear on the customer site on the next page load. Pause or resume the campaign.
- [ ] The audit log shows every admin action and never shows an access code.
- [ ] Change password works. The old password stops working.
- [ ] Restart the app while signed in as admin, then reload `/admin` in the same tab: you should still be signed in.

## Post-deploy on MonsterASP

Run on the live URL after every deploy (see `docs/MONSTERASP_DEPLOY.md`).

- [ ] `/health` returns `Healthy`.
- [ ] Before HTTPS: the warning banner shows on the customer and admin pages. **Use test data only.**
- [ ] After HTTPS: `http://` redirects to `https://`, there's no banner, and the browser shows the cookies as `Secure`.
- [ ] Admin login works, and the `AdminBootstrap__*` variables have been **removed**.
- [ ] Create a test buyer → copy the code → customer login on a phone.
- [ ] Spin through Try Again and a real win (temporarily raise a cheap prize's odds if needed, then restore them).
- [ ] Refresh after the result: it's the same result. Admin `/admin/spins` and `/admin/awards` show it.
- [ ] Grant a credit → the customer sees it after a reload. Mark the award delivered.
- [ ] Prize photos and the logo load, and the Share PNG downloads.
- [ ] **Restart** the site in the Monster panel. The customer **and the admin** are still signed in (Data Protection keys in SQL), and data is intact.
- [ ] Repeat the same spin request (e.g. reload during a spin): there's no double debit.
- [ ] Delete test customers, set the real allocation, turn SQL Remote access **off**, and take a manual backup.

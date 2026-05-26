# bigTest Re-Audit (follow-up) — ESEMS.Web (ejraa360)
**Date:** 2026-05-23
**Mode:** static (read-through) + targeted runtime spot-checks (the new password flow was exercised live)
**Auditor:** Claude (bigTest skill)
**Scope:** delta since the 2026-05-23 baseline audit (commit f70f069 + fa3b7f2 + 8ea7bb6). The full system was audited and remediated on 2026-05-23 (29 findings, all closed); this pass re-sweeps RBAC/IDOR system-wide and scrutinizes the NEW code: `AccountController.ChangePassword`, `ExcelImportService` MBRHE import-order, the IDOR scope guards, and the Profile redesign.

## Executive summary
- **New findings: 5** — Critical: 0, High: 0, Medium: 2, Low: 3.
- The remediated baseline holds. The IDOR guard sweep is **near-complete** — every by-id action and every `Get*`-by-id helper across the 6 named controllers + Processes/Services is scope-gated **except two** (FU-001). The new password-change code is sound (authed, CSRF, current-password verify, PBKDF2 re-hash, rate-limited) with three hardening gaps (FU-002/003/005) and one UX gap (FU-004).
- **Top 3:**
  1. `WorkloadAnalysis/GetProcessData` + `GetServiceData` leak an out-of-scope process/service's name + metric to any unit-scoped `Workload.View` user — the only two `Get*` helpers that miss the scope check every sibling has (FU-001).
  2. Password change doesn't invalidate other active sessions — a stolen auth cookie survives a password change (FU-002).
  3. No server max-length on the new password — a multi-megabyte password drives PBKDF2 CPU cost (FU-003).

---

## Findings — Medium

### FU-001: WorkloadAnalysis `GetProcessData` / `GetServiceData` skip the per-record scope check (IDOR)
- **Page:** GET `/WorkloadAnalysis/GetProcessData?processId=…`, GET `/WorkloadAnalysis/GetServiceData?serviceId=…`
- **Role(s):** any unit-scoped user with `Workload.View` (e.g. Process Owner / Improvement Analyst RoleGroup)
- **Severity:** Medium · **Nielsen:** N/A · **Standard:** RBAC record-level (IDOR)
- **Steps:** As a unit-scoped user, GET `/WorkloadAnalysis/GetProcessData?processId={id-of-another-unit's-process}`.
- **Expected:** `NotFound()` after `scope.CanAccess(process)` — exactly as the sibling helpers do (`Details`, `AddLineItem`, and every `Get*` in Processes/Services/Assets/Improvements).
- **Actual:** returns `{ nameEn, nameAr, durationMinutes }` (process) / `{ nameEn, nameAr, annualVolume }` (service) for ANY id, with no scope probe. Bounded leak (name + one metric) but the same class as F-003/F-006/F-008, and the only two `Get*`-by-id endpoints in the app that omit the pattern. Missed by the baseline audit (not in the F-003 action list).
- **Source:** `Controllers/WorkloadAnalysisController.cs:588` (GetProcessData), `:606` (GetServiceData). Process & Service are `IOwnedByUnit`; the probe is `scope.CanAccess(process)` / `scope.CanAccess(service)`.

### FU-002: Password change does not invalidate other active sessions
- **Page:** POST `/Account/ChangePassword`
- **Role(s):** all
- **Severity:** Medium · **Standard:** session management / OWASP ASVS 3.3
- **Actual:** auth is plain cookie auth with no security-stamp / session-version validation. After a user changes their password, any *other* already-issued auth cookie (e.g. one an attacker captured) remains valid until it expires — the password change doesn't revoke it. ASP.NET Identity solves this with a SecurityStamp validated on each request; this app's custom cookie auth has no equivalent.
- **Source:** `Controllers/AccountController.cs` ChangePassword (no stamp bump); `Program.cs` cookie auth (no `OnValidatePrincipal` stamp check); `Models/CustomUser.cs` (no security-stamp column).
- **Note:** the fix needs a schema column + a cookie-validation event — i.e. a migration + an auth-pipeline change. Deferred from this fix pass (changes the "no-migration / plain-ZIP redeploy" story right before a deploy); recommend a dedicated follow-up.

---

## Findings — Low

### FU-003: No server-side max length on the new password (PBKDF2 CPU)
- **Page:** POST `/Account/ChangePassword`
- **Severity:** Low · **Standard:** DoS / resource-exhaustion (OWASP)
- **Actual:** only `minlength=8` is enforced. PBKDF2 HMAC cost scales with input length × iterations (100k), so a multi-megabyte "password" turns one request into meaningful CPU. Cap the length (e.g. 128).
- **Source:** `Controllers/AccountController.cs` ChangePassword validation; `Views/Account/Profile.cshtml` (`#newPw` has `minlength` but no `maxlength`).

### FU-004: Change-password form has no client-side confirm/strength feedback
- **Page:** `/Account/Profile` (Security tab)
- **Severity:** Low · **Nielsen:** minor · **Standard:** H9 (recognize/recover from errors)
- **Actual:** "new" vs "confirm" mismatch and the min-length rule are only reported after a full server round-trip (`minlength` covers length client-side, but not the match). A quick client check gives immediate feedback.
- **Source:** `Views/Account/Profile.cshtml` change-password form.

### FU-005: No user alert when the password changes
- **Page:** POST `/Account/ChangePassword`
- **Severity:** Low · **Standard:** account-security best practice (notify-on-change)
- **Actual:** the change is written to `AuditLog` (good) but the user gets no proactive alert (email/in-app), so a malicious change from a hijacked session goes unnoticed until the user next tries their old password. Recommend an in-app notification (`INotificationService`) or email (`SmtpEmailService`, best-effort) to the account owner.
- **Source:** `Controllers/AccountController.cs` ChangePassword.

---

## What re-verified clean
- **IDOR sweep:** Details/Edit/Delete/Calculate/Clone/Export/Compare/BulkReassign/line-items in WorkloadAnalysis all gate on `scope.CanAccess`; Processes `GetBpmnVersions`/`GetBpmnVersion`/`GetAvailableServices` gate via `ProcessScopeProbe` (SEC-008); Assets/Improvements/Services `Get*` helpers gate via their `CanAccess*` probes (F-006/F-008); AIBpmnReadController scopes via parent process (SEC-003). Only FU-001 remains.
- **ChangePassword:** `[Authorize]` + `[ValidateAntiForgeryToken]` + explicit route; current-password verified before any mutation; PBKDF2 re-hash; per-user brute-force lockout (5/15 min); operates on the caller's OWN claim (no user-id parameter → no cross-user IDOR); legacy SHA-256 → PBKDF2 upgrade on change (runtime-verified).
- **F-023 import order:** fails fast with nothing saved only when org table is empty AND a specific owner is named; partial imports still proceed — correct.
- **Profile F-013/F-014:** form is a plain POST (CSP-safe); full ARIA tab pattern present (`role=tabpanel`, `aria-selected`, `aria-controls`, roving `tabindex`, arrow keys) — confirmed in rendered HTML at runtime.

## Methodology
Static read-through of the changed surface + system-wide grep of every `Get*`-by-id and by-id action for `scope.CanAccess`/`GetScopeAsync`; adversarial read of `ChangePassword`; runtime spot-check of the live password-change flow (login → change → re-login). Limitations: no full per-role dynamic click-through (the baseline dynamic gaps remain); no load test (separate `dotnet-full-test` concern).

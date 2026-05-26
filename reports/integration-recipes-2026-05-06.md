# External Integration Recipes

**Generated:** 2026-05-06.
**Scope:** every external system this codebase can talk to, plus the
**exact** config knobs to enable each one. Everything is OFF by default
in `appsettings.json`; flipping one on requires a per-environment override
(`appsettings.Production.json` or env vars) and an IIS app pool recycle.

## Why this exists

The bigTest skill flagged "external integrations not exercised" as a
deferred finding because none of these gateways are configured in dev.
This doc gives the operator the knob locations + smoke-test recipe so
turning one on is mechanical, not a code review.

---

## 1. SMTP / outbound email

**Service:** `Services/Email/SmtpEmailService.cs` (registered in
`Program.cs:95`).
**Templates:** bilingual EN/AR built inline by the calling controller —
no template engine, no separate Arabic asset to localize.
**Default state:** `Email.Enabled = false`. Every `SendAsync` call logs
`"Email disabled — skipping send to {To}"` and returns `false`.

### Enable for production
1. In `appsettings.Production.json`, set:
   ```json
   "Email": {
     "Enabled": true,
     "Host": "smtp.office365.com",
     "Port": 587,
     "EnableSsl": true,
     "FromAddress": "noreply@mbrhe.gov.ae",
     "FromName": "ESEMS",
     "TimeoutSeconds": 15
   }
   ```
2. Set credentials via env vars (NEVER commit them):
   ```
   Email__Username=<smtp-user>
   Email__Password=<smtp-pass>
   ```
3. Recycle the IIS app pool.
4. Smoke test: log in as admin → Settings Hub → Email Alerts → "Send
   test email". Watch the inbox + the controller log line
   `"Email sent to {To} subject={Subject}"`.

### Smoke-test failures to watch for
- **AuthenticationFailed** → username/password wrong, or 2FA on the
  service account (use an app password).
- **ServerHostnameUnreachable** → check IIS host's outbound 587, often
  blocked on AWS EC2 by default.
- **Mojibake in Arabic body** → check `Content-Type` header is
  `text/html; charset=utf-8` (default, but verify if a proxy strips it).

### Test coverage
`ESEMS.Tests/Services/SmtpEmailServiceTests.cs` covers config-disabled
paths + send-on-success. Bilingual rendering is **not** unit-tested —
no MailKit fake captures the rendered MIME body. Trust the manual
smoke until a real receiving-side capture is added.

---

## 2. UAE Pass (federal digital identity)

**Status:** referenced in AI prompts as a **future** automation
opportunity. **No client code or auth handler exists in this repo.**
Searches for `UaePass` / `OAuth` / `OpenIdConnect` outside the AI
prompt strings return zero hits.

### To integrate (greenfield work)
1. Add the [UAE Pass OIDC scheme](https://docs.uaepass.ae/) via
   `Microsoft.AspNetCore.Authentication.OpenIdConnect`.
2. Register a second auth scheme alongside the existing cookie auth in
   `Program.cs`. Map the UAE Pass `sub` claim → existing
   `CustomUser.UaeId` (will need a new column).
3. Add a "Sign in with UAE Pass" button on `/Account/Login`.
4. Test in UAE Pass sandbox first; production goes live only with a
   government-issued client ID.

This is product work, not configuration — escalate to a sprint, not an
operator runbook.

---

## 3. SMS gateway

**Status:** **no SMS provider integrated.** Searches for
`Twilio` / `SmsService` / `Etisalat` / `Du` return zero hits.

### To integrate
Mirror the `IEmailService` pattern: introduce `ISmsService` +
`TwilioSmsService` (or whatever provider), register in `Program.cs`,
gate behind an `Sms.Enabled` config flag, and wire into
`NotificationService` so the existing per-channel opt-out works.

---

## 4. Payment gateway

**Status:** **no payment provider integrated.** ESEMS doesn't take
payments — it manages improvements / processes / risks / change
requests. If a future module needs payments, treat it as a new
integration project (UAE Network International / Telr / Stripe) rather
than a config change.

---

## 5. Risk integration (external Risk system)

**Service:** `Services/Integrations/Risk/IRiskProvider.cs` plus
`RestRiskProvider` (when enabled). Adapter pattern so the provider can
be swapped per tenant.
**Default state:** `Integrations.Risk.Provider = "None"` — the provider
returns no rows; the EnterpriseRisks pages render the LOCAL DB rows
unchanged.

### Enable
In `appsettings.Production.json`:
```json
"Integrations": {
  "Risk": {
    "Provider": "Http",
    "BaseUrl": "https://risk.example.gov.ae/api",
    "DisplayName": "Enterprise Risk Hub",
    "DeepLinkBaseUrl": "https://risk.example.gov.ae/risks",
    "TimeoutSeconds": 8,
    "CacheSeconds": 60
  }
}
```
Plus env var `Integrations__Risk__ApiKey=<token>` (never commit).
Recycle. Verify the EnterpriseRisks Index page renders external rows
with the configured DisplayName badge.

When Provider="Http" the local-write pages are taken over by the
external system — see `project_integration` memory note for the
takeover semantics.

---

## 6. Process performance integration

**Service:** `Services/Integrations/ProcessPerformance/` — same
adapter pattern as Risk.
**Default state:** `Integrations.ProcessPerformance.Provider = "None"`.

Same enable recipe as Risk, swap section name. Recycle. Verify the
Processes Details page shows performance metrics from the external
system.

---

## 7. AzureOpenAI (already wired)

**Service:** `Services/AI/AzureOpenAIService.cs`.
**Status:** wired but credentials empty by default.

### Enable
In `appsettings.Production.json` (or env):
```
AzureOpenAI__Endpoint=https://<resource>.openai.azure.com/
AzureOpenAI__ApiKey=<key>
AzureOpenAI__DeploymentId=gpt-4o
```
Recycle. Test via `/AI/ProcessAnalyzer` or the floating AI bubble in
the dashboard.

---

## 8. AWS Bedrock (already wired)

**Service:** `Services/AI/BedrockService.cs`.
**Default region:** `us-east-1`.

Auth is via standard AWS SDK — IAM role assigned to the EC2 instance,
or `AWS_ACCESS_KEY_ID` + `AWS_SECRET_ACCESS_KEY` env vars. No
ESEMS-specific config beyond `AWS.Region`.

---

## What's NOT integrated (and shouldn't be assumed)

- **DLD** (Dubai Land Department), **Ejari**, **MOI**, **MOHRE** — the
  AI prompts mention them as **example** federal systems an AI analysis
  might suggest connecting to, but no code path actually calls any of
  them. Don't promise a customer "ESEMS connects to MOI" without first
  building the integration.
- **GlobalProtect VPN** — operational concern (the IIS host may sit
  behind it), not an in-app integration.

---

## Operator quick-check

After enabling any integration, run:

```bash
curl -sI https://<your-app>/health  # 200 if app pool started cleanly
```

Then exercise the feature path the integration powers and tail the
app pool's stdout (or `Application` event log) for the corresponding
service's "{Provider} call succeeded / failed" log line.

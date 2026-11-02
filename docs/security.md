# Security and operational notes

Health ERP is a portfolio application, not a certified clinical system. Use synthetic data for local development and public demos.

## Private configuration

Keep database, JWT, SMTP and optional development-seed credentials in user secrets or environment configuration. The tracked example configuration contains placeholders only. Angular bundles are public and must never contain server credentials. A root .env file is not automatically loaded.

Use strong random signing material and configured issuer/audience. The API rejects missing/short/example signing keys and missing/example database configuration. Reset/activation delivery logs contain operational outcomes, never raw authentication secrets. Contact-form submissions must not be logged.

## Demo data and privacy

Development startup applies migrations and initializes roles. Accounts/catalogs require explicit DevelopmentSeed opt-in and privately configured account credentials. Existing accounts are not automatically reset. Synthetic triage seeding can attach to an existing patient's ID: enable it only in a disposable database, never against clinical data. Never copy real charts, contacts, payments, tokens or email captures into fixtures/screenshots.

Test records are constructed scenario data. Reserved example domains and demo labels describe fictional identities. The landing illustration is static demo content, not an actual patient's chart. Contact-form dispatch is a UI simulation; it is not a backend support-ticket integration.

## Authorization boundaries

The Patient Portal enforces account ownership. Doctor Workspace requires a live linked Doctor Staff identity and qualifying appointments for patient access and note writes. Shared patient APIs use broader role policies; review all object-level permissions before operational deployment. JWT state currently resides in localStorage, so XSS prevention and a deployment-specific session/CSP review are important.

## Remaining production hardening

Review account/IP abuse controls, static-salt low-entropy OTP protection, booking exclusion constraints, write idempotency, CORS/HTTPS configuration, and real SMTP delivery. Audit actor/timestamps are not a comprehensive immutable clinical audit trail. Replace dashboard demonstration fallbacks before operational use. Define explicit migration/role provisioning, monitoring, access review, backup/recovery and incident procedures before deployment.

For any credential exposure, revoke/rotate affected credentials and sessions and review retained copies/logs. Source cleanup does not rotate external credentials. No deployment workflow is included.

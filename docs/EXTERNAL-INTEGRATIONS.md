# External integration boundary

## Moodle

The repository supports two configured launch modes:

- `ExternalLink` when only `MOODLE_BASE_URL` is supplied.
- `SignedLaunch` when `MOODLE_BASE_URL` and `MOODLE_SSO_SHARED_SECRET` are supplied. The launch contains the DigitMak user id, e-mail, display name, language, timestamp and an HMAC-SHA256 signature.

The Moodle owner must still confirm which Moodle-side authentication plugin or protocol will validate the launch. A production SSO claim cannot be made until that protocol, its callback/launch URL and the shared secret or OIDC/SAML credentials are supplied and tested on the target Moodle installation.

## Google and Microsoft calendars

V1 provides private ICS exports for the signed-in client and the authorised staff calendar. These files can be imported by Google Calendar, Microsoft Outlook and other RFC 5545-compatible calendar clients.

Native account synchronisation is intentionally marked as V2, matching the PDF. Implementing it requires a provider decision and protected values that do not belong in the repository: Google OAuth client id/secret, Microsoft Entra application and tenant details, approved redirect URIs, consented scopes, token-encryption keys and the intended one-way or two-way synchronisation rules.

Until those inputs exist, the portal reports ICS import as available and native synchronisation as disabled instead of presenting an unverified external connection as production-ready.

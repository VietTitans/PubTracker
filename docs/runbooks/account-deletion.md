# Account deletion: what is removed, and where data can remain

A user who deletes their account (or is purged for 5 years of inactivity) is marked, kept for the 30-day grace period (signing back in undoes it), then removed by the daily purge (`UserPurgeBackgroundService`). The Keycloak account is deliberately not touched at soft-delete time, because signing back in is the undo path.

## What the purge does

For each user past the grace period, in this order:

1. Deletes their **Keycloak account** (email and identity) through the Admin API. A 404 counts as success (already gone). Any other failure is logged (`Could not delete the Keycloak account of user ...`) and that user is **left in place**; the next purge cycle retries. A user with no Keycloak account on file skips this step.
2. Deletes their database rows: Subscriptions, digest watermarks, the user row, and (through `ON DELETE CASCADE`) their outbox rows.

Keycloak goes first so that a user can never lose their row while their identity still exists, which would let a later login silently re-provision them.

The Keycloak service account (`pubtracker-api-admin`) needs `manage-users`, which already covers deleting users.

If the Keycloak admin client secret is not configured, the app uses `NullKeycloakUserSync` and skips step 1. Production must have it set.

## Brevo

- **Contacts: none.** `BrevoEmailSender` only calls the transactional endpoint (`POST /v3/smtp/email`). It never creates Brevo contacts, so there is nothing to delete there.
- **Transactional logs and email previews: yes, and they are kept indefinitely by default.** Brevo stores the recipient address and the full email content (a Digest lists record titles and abstracts). Keycloak's verification and reset mails go through Brevo SMTP and are logged the same way. Brevo's own help centre describes the retention setting under Settings > Transactional emails > Retention rules (source: [Manage the retention period of transactional logs and email previews](https://help.brevo.com/hc/en-us/articles/4415743225746), read 2026-10-20; re-check it).
- The purge does **not** delete these logs. Set a retention rule so they age out on their own. Choose a period that covers the privacy policy's stated figure for deleted accounts.

### One-time setup (manual, in the Brevo dashboard)

- [ ] Set the transactional logs and email previews retention rule (date, who, period chosen)

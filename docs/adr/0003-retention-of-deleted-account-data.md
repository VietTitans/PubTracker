# Deleted-account data is kept up to about 44 days, and the privacy policy says so

A deleted account is soft-deleted and recoverable for 30 days (`UsersDataAccess.DeletionGracePeriodDays`). After that `UserPurgeBackgroundService` hard-deletes the user, their Subscriptions and their Keycloak account. Nightly encrypted database backups in EU Cloudflare R2 are kept for 14 days (`docs/runbooks/backup-restore.md`), so the user's data can still sit in a backup for 14 more days. The privacy policy (`/privacy`) therefore states a worst case of about 44 days after a deletion request.

We keep this rather than rewriting backups on deletion because backups are encrypted, held in the EU, and expire on their own within 14 days. Rewriting them would add machinery with little privacy gain for a small service.

## Consequences

- Changing the grace period or the backup retention changes the figure in `web/src/pages/LegalPages.tsx`; update both.
- Brevo transactional logs are not deleted by the purge. Set their retention rule to cover this figure (`docs/runbooks/account-deletion.md`).
- Data export is manual: the user emails the privacy address and we answer within 30 days. Build a self-service export if requests become frequent.
- The owner must review the policy text; it is not legal advice.

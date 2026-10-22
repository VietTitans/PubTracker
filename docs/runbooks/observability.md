# Observability and alerts

Silent failures should reach the owner. Four things watch the system, and each one alerts through the channels at the bottom.

| What | Catches | Where it lives |
| --- | --- | --- |
| Sentry (EU) | Unhandled errors, and the Brevo quota alert (a logged error) | API code; project in Sentry |
| Heartbeat: poll worker | The poll worker stalled or crashed | Healthchecks.io check, pinged by the API |
| Heartbeat: outbox worker | Digests stop being sent | Healthchecks.io check, pinged by the API |
| Heartbeat: backup | A missed nightly backup | Healthchecks.io check, pinged by `backup.sh` (see `backup-restore.md`) |
| Uptime check | The site or `/api/health` is down from the outside | External monitor (not in the VM) |

The code side is switched on by settings in the VM `.env` (see `docker/.env.prod.example`): `SENTRY_DSN`, `HEARTBEAT_POLL_URL`, `HEARTBEAT_OUTBOX_URL`. Blank means off. Everything below is set up by hand in each service, because it needs your accounts.

## Sentry

- Create a Sentry project in the **EU region** (data stays in the EU; the DSN host ends in `ingest.de.sentry.io`). Put the DSN in `SENTRY_DSN`.
- **Personal data.** The app sends none: `SendDefaultPii` is off, only warnings and errors become breadcrumbs, and `SentryScrubber` removes user identity, IP, request bodies, cookies, headers, unsubscribe tokens in URLs, and any email address in a message. As a second layer, in the project settings turn on **Prevent Storing of IP Addresses** and **Data Scrubber** (Settings > Security & Privacy).
- **Alert rule.** Create an issue alert: "a new issue is created" and "an issue changes state from resolved to unresolved", sent to the owner by **email and mobile push** (install the Sentry mobile app and enable push for the project). Mark an issue **resolved** once handled; the same error then alerts again when it returns (the quota alert below needs this to alert on a later day).
- **Test from production** (after a deploy, once):
  ```sh
  cd ~/pubtracker
  docker compose -f docker-compose.prod.yml exec api dotnet RecordService.dll sentry-test
  ```
  Open the event in Sentry and check that the user, IP, request and headers sections are empty.

## Worker heartbeats (Healthchecks.io)

Healthchecks.io already holds the backup check. Add two more, copy each ping URL into `.env`, and redeploy.

| Check | Env var | Period | Grace |
| --- | --- | --- | --- |
| Poll worker | `HEARTBEAT_POLL_URL` | the poll interval (`SCHEDULER_POLL_INTERVAL_HOURS`, default 168 h = 7 days) | 1 day |
| Outbox worker | `HEARTBEAT_OUTBOX_URL` | 10 minutes | 10 minutes |

A worker pings after each cycle that finishes without an exception, so a stalled worker and a worker that keeps throwing both stop pinging and raise the alert. The outbox worker pings at most every 5 minutes (`Heartbeat:MinIntervalSeconds`).

**Test that a stopped worker alerts** (once, after setup): `docker compose -f docker-compose.prod.yml stop api`, wait for the outbox check's period and grace, confirm the alert arrives on every channel, then `start api`. The pinging itself is covered by `ObservabilityTests` in the test suite.

## Uptime check

Use an external monitor so that it also notices the VM, the tunnel or Cloudflare being down (Healthchecks.io and Sentry cannot, because the app itself would be what is missing). A free monitor (UptimeRobot, Better Stack, or similar):

- URL: `https://pubtracker.org/api/health` (checks the database too), every 1-5 minutes, expect HTTP 200.
- Alert after 2 failed checks, by email and mobile push.

## Brevo quota alert

The digest outbox sends at most `EMAIL_DAILY_SEND_LIMIT` (250) Digests a day, and Brevo's free tier allows 300 emails a day shared with Keycloak's verification and reset mails. When the day's sent count reaches **80% of that limit** the API logs one error ("Digest sending reached ... emails allowed per UTC day (80% alert)"), once per UTC day. Sentry turns that into an alert through the rule above. No Brevo dashboard setting is needed.

## Alert channels

The alert must not depend on a mailbox on the project domain (it would go down with the project). Every service sends to **a personal mailbox on a different provider** and to **phone push**.

| Service | Email | Phone push |
| --- | --- | --- |
| Sentry | | Sentry mobile app |
| Healthchecks.io | | Healthchecks mobile app, or Pushover / ntfy integration |
| Uptime monitor | | the monitor's app, or Pushover / ntfy |

Fill in the table when set up (which address, which app).

## Setup checklist

- [ ] Sentry EU project created, `SENTRY_DSN` set, scrubbing options on, alert rules created
- [ ] `sentry-test` run from production; the event has no personal data
- [ ] Healthchecks checks for the poll and outbox workers created, URLs in `.env`
- [ ] Stopped-worker alert tested (date, who, result)
- [ ] Uptime monitor live and alerting on a forced failure
- [ ] Alert channels table filled in

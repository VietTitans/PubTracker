# Keycloak admin access and realm hardening

The public hostname does not serve the Keycloak admin console or the master realm (`docker/caddy/Caddyfile.prod` answers 404 for `/auth/admin*` and `/auth/realms/master*`). Keycloak's port 8080 is published on the VM's loopback only, and `KC_HOSTNAME_ADMIN` points the console at `http://localhost:8080/auth`.

## Admin console via SSH tunnel

```
ssh -L 8080:127.0.0.1:8080 <user>@ssh.pubtracker.org      # same ProxyCommand as deploy.sh
```
Then open `http://localhost:8080/auth/admin/` and log in as `admin` (`KEYCLOAK_ADMIN_PASSWORD`). Verify the block: `curl -i https://pubtracker.org/auth/admin/` returns 404.

## What the realm file sets

`docker/keycloak/realm-export.prod.json` is imported only when the realm does not exist yet. It sets Brevo SMTP (`smtp-relay.brevo.com:587`, STARTTLS; credentials `KEYCLOAK_SMTP_USER` / `KEYCLOAK_SMTP_PASSWORD` in `.env`, from Brevo > SMTP & API > SMTP, not the API key), required email verification, brute-force protection, and the `pubtracker-api-admin` service account with `view-users` and `manage-users`.

## Applying this to the already-imported production realm

The import does not overwrite an existing realm, so change it in the console (tunnel above), in this order:

1. Realm settings > Email: enter the Brevo SMTP settings and use **Test connection**. Confirm the test mail arrives.
2. Only then Realm settings > Login: switch **Verify email** on. (Switching it on before SMTP works locks new signups out.)
3. Realm settings > Security defenses > Brute force detection: enable.
4. Users > `service-account-pubtracker-api-admin` > Role mapping: the user must be the service account of client `pubtracker-api-admin` with `view-users` and `manage-users` from `realm-management`.
5. Sign up a new test user and confirm the verification mail is delivered.

Or, if the production realm holds no real users yet, delete it in the console and restart Keycloak to re-import the file.

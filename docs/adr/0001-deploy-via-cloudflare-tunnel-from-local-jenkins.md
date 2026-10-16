# Deploy through a Cloudflare Tunnel from a local Jenkins, with no inbound ports

Production runs on one Oracle A1 (arm64) VM. A local Jenkins builds `linux/arm64` images with `buildx`, pushes them to GHCR, and deploys over SSH; both the public site (`pubtracker.org`, proxied) and the SSH endpoint (`ssh.pubtracker.org`, behind Cloudflare Access) reach the VM through a `cloudflared` tunnel, so the Oracle firewall keeps every inbound port closed. We chose this over GitHub Actions (native arm64 runners, but wanted Jenkins for now) and over open SSH with Let's Encrypt (simpler, but exposes ports 22/80/443). Jenkins is expected to move onto a VM later, so deploy logic lives in repo scripts that the `Jenkinsfile` only calls.

## Consequences

- Builds and deploys only run while the Jenkins PC is on; Jenkins polls `main` because GitHub cannot reach it.
- The arm64 build runs under QEMU on the Jenkins host and is slow; if it becomes a problem, build on the VM or move CI to arm64 hardware.
- App secrets live only in a hand-made `.env` on the VM; Jenkins holds just the SSH/Access token and GHCR token.
- Rollback is a manual redeploy of an older build-number tag; schema migrations run on API start and have no automatic down path.

# Custom Jenkins controller image: stock LTS image + the docker CLI (client only — no
# daemon), so Jenkinsfile can run `docker build` / docker.image().inside() against the
# host's Docker daemon via the socket mounted in jenkins/docker-compose.yml.
# Also: buildx (arm64 prod images), ssh + cloudflared (deploy/deploy.sh reaches the VM through a tunnel).
FROM docker:27-cli AS docker-cli

FROM jenkins/jenkins:2.579
USER root
COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
COPY --from=docker-cli /usr/local/libexec/docker/cli-plugins/docker-buildx /usr/local/lib/docker/cli-plugins/docker-buildx
COPY --from=cloudflare/cloudflared:2026.10.3 /usr/local/bin/cloudflared /usr/local/bin/cloudflared
RUN apt-get update && apt-get install -y --no-install-recommends openssh-client && rm -rf /var/lib/apt/lists/*
USER jenkins

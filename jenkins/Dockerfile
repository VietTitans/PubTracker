# Custom Jenkins controller image: stock LTS image + the docker CLI (client only — no
# daemon), so Jenkinsfile can run `docker build` / docker.image().inside() against the
# host's Docker daemon via the socket mounted in jenkins/docker-compose.yml.
FROM docker:27-cli AS docker-cli

FROM jenkins/jenkins:2.579
USER root
COPY --from=docker-cli /usr/local/bin/docker /usr/local/bin/docker
USER jenkins

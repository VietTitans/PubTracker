pipeline {
    agent any

    options {
        timestamps()
        disableConcurrentBuilds()
    }

    // GitHub can't reach a local Jenkins, so poll (docs/adr/0001).
    triggers { pollSCM('H/5 * * * *') }

    environment {
        PUBLIC_ORIGIN = 'https://pubtracker.org'
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_NOLOGO = '1'
        // Restore/Build/Test each run in a fresh, throwaway SDK container. NuGet's default
        // cache (~/.nuget/packages) lives inside that container and is gone once it exits, so
        // point it at the Jenkins workspace instead — that directory is bind-mounted from the
        // host into every .inside() container at the same path, so it persists across stages.
        NUGET_PACKAGES = "${WORKSPACE}/.nuget-packages"
        // Testcontainers' Ryuk cleanup sidecar can't be reached back over the network in this
        // Docker-outside-of-Docker setup (SDK container -> host daemon via mounted socket), so
        // its init just times out ("Initialization has been cancelled"). Standard CI workaround:
        // disable it. Tradeoff: if a test crashes mid-run, its ephemeral Postgres container won't
        // be auto-removed — the Test stage's post { always } block removes them instead.
        TESTCONTAINERS_RYUK_DISABLED = 'true'
        // Testcontainers guesses 172.17.0.1 (the classic Linux docker0 bridge gateway) as the
        // address to reach sibling containers' published ports, which doesn't reliably work
        // under Docker Desktop's networking model on Windows/Mac. host.docker.internal is
        // Docker Desktop's own DNS name for the host, resolves automatically in any container,
        // and is where published ports actually are reachable from.
        TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal'
        // Same reason as NUGET_PACKAGES: the node container's home is not writable and is thrown away.
        npm_config_cache = "${WORKSPACE}/.npm-cache"
    }

    stages { 
        stage('Checkout') {
            steps {
                checkout scm
            }
        }

        stage('Restore') {
            steps {
                script {
                    docker.image('mcr.microsoft.com/dotnet/sdk:10.0').inside('-v /var/run/docker.sock:/var/run/docker.sock') {
                        sh 'dotnet restore api/test/Test.csproj'
                    }
                }
            }
        }

        stage('Build') {
            steps {
                script {
                    // Test.csproj has a ProjectReference to RecordService.csproj (which references
                    // RecordData.csproj), so building it transitively builds the whole api/ solution —
                    // no separate RecordService-only build step needed (no .sln file exists; projects
                    // are referenced directly by path throughout).
                    docker.image('mcr.microsoft.com/dotnet/sdk:10.0').inside('-v /var/run/docker.sock:/var/run/docker.sock') {
                        sh 'dotnet build api/test/Test.csproj --no-restore -c Release'
                    }
                }
            }
        }

        stage('Test') {
            steps {
                script {
                    // Excludes BrevoEmailSenderManualTest / PedroProviderManualTest, which hit real
                    // external services (Brevo email API, live PEDro site + headless browser).
                    docker.image('mcr.microsoft.com/dotnet/sdk:10.0').inside('-v /var/run/docker.sock:/var/run/docker.sock') {
                        sh '''
                            dotnet test api/test/Test.csproj \
                                --no-build -c Release \
                                --filter "FullyQualifiedName!~ManualTest" \
                                --logger "trx;LogFileName=test-results.trx" \
                                --results-directory ./TestResults
                        '''
                    }
                }
            }
            post {
                always {
                    archiveArtifacts artifacts: 'TestResults/*.trx', allowEmptyArchive: true
                    // Ryuk is disabled (see environment), so remove Testcontainers' containers ourselves.
                    // Testcontainers labels everything it starts with org.testcontainers=true.
                    sh 'docker ps -aq --filter label=org.testcontainers=true | xargs -r docker rm -f -v'
                }
            }
        }

        stage('Web Install and Build') {
            steps {
                script {
                    // `npm run build` is `tsc -b && vite build`, so a type error fails the build here
                    // instead of inside the image build. VITE_* are only baked into the images below.
                    // The Node image is read from web/Dockerfile's build stage, so CI and the image
                    // always use the same Node version.
                    def nodeImage = sh(script: '''awk '/^FROM node:.* AS build/ {print $2}' web/Dockerfile''', returnStdout: true).trim()
                    if (!nodeImage) { error 'No "FROM node:... AS build" line found in web/Dockerfile' }
                    docker.image(nodeImage).inside {
                        sh 'cd web && npm ci && npm run build'
                    }
                }
            }
        }

        // Every branch's images are pushed (tagged with the build number) so a branch can be
        // pulled and tried; only main is deployed.
        stage('Build and Push Images') {
            steps {
                // Prod VM is linux/arm64; buildx emulates it via QEMU (one-time on the Jenkins host:
                // docker run --privileged --rm tonistiigi/binfmt --install arm64).
                // The Jenkins container's docker CLI has no buildx plugin; docker:cli bundles it.
                withCredentials([usernamePassword(credentialsId: 'ghcr-token', usernameVariable: 'GHCR_USER', passwordVariable: 'GHCR_TOKEN')]) {
                  script {
                    docker.image('docker:cli').inside('-v /var/run/docker.sock:/var/run/docker.sock') {
                    sh '''
                        OWNER=$(echo "$GHCR_USER" | tr '[:upper:]' '[:lower:]')
                        echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USER" --password-stdin
                        docker buildx inspect pt-builder >/dev/null 2>&1 || docker buildx create --name pt-builder --driver docker-container
                        docker buildx build --builder pt-builder --platform linux/arm64 --push \
                            -f api/Dockerfile -t ghcr.io/$OWNER/pubtracker-api:$BUILD_NUMBER api
                        # Vite bakes VITE_* in at build time; API and Keycloak are same-origin behind Caddy.
                        docker buildx build --builder pt-builder --platform linux/arm64 --push \
                            --build-arg VITE_API_BASE_URL=$PUBLIC_ORIGIN \
                            --build-arg VITE_KEYCLOAK_AUTHORITY=$PUBLIC_ORIGIN/auth/realms/science-alerts-saas \
                            --build-arg VITE_KEYCLOAK_CLIENT_ID=science-alerts-web \
                            -f web/Dockerfile -t ghcr.io/$OWNER/pubtracker-web:$BUILD_NUMBER web
                    '''
                    }
                  }
                }
            }
        }

        stage('Deploy') {
            when { expression { (env.BRANCH_NAME ?: env.GIT_BRANCH) ==~ /(.*\/)?main/ } }
            steps {
                withCredentials([
                    sshUserPrivateKey(credentialsId: 'deploy-ssh-key', keyFileVariable: 'SSH_KEY', usernameVariable: 'DEPLOY_USER'),
                    usernamePassword(credentialsId: 'cf-access-token', usernameVariable: 'TUNNEL_SERVICE_TOKEN_ID', passwordVariable: 'TUNNEL_SERVICE_TOKEN_SECRET')
                ]) {
                    sh 'sh deploy/deploy.sh $BUILD_NUMBER'
                }
            }
        }
    }
}

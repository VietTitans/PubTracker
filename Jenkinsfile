pipeline {
    agent any

    options {
        timestamps()
        disableConcurrentBuilds()
    }

    environment {
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
        // be auto-removed — harmless locally, just prune manually if containers pile up.
        TESTCONTAINERS_RYUK_DISABLED = 'true'
        // Testcontainers guesses 172.17.0.1 (the classic Linux docker0 bridge gateway) as the
        // address to reach sibling containers' published ports, which doesn't reliably work
        // under Docker Desktop's networking model on Windows/Mac. host.docker.internal is
        // Docker Desktop's own DNS name for the host, resolves automatically in any container,
        // and is where published ports actually are reachable from.
        TESTCONTAINERS_HOST_OVERRIDE = 'host.docker.internal'
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
                }
            }
        }

        stage('Build API Docker Image') {
            steps {
                // Context is api/ (Dockerfile does `COPY . .` from WORKDIR /src and references
                // RecordService/RecordService.csproj relatively) — matches how docker/docker-compose.yml
                // already builds it (build.context: ../api).
                sh "docker build -f api/Dockerfile -t pubtracker-api:${env.BUILD_NUMBER} api"
            }
        }

        stage('Build Web Docker Image') {
            steps {
                // Build args match the defaults docker/docker-compose.yml passes to this same
                // Dockerfile; Vite bakes VITE_* vars into the static bundle at build time.
                sh """
                    docker build -f web/Dockerfile \
                        --build-arg VITE_API_BASE_URL=http://localhost:8080 \
                        --build-arg VITE_KEYCLOAK_AUTHORITY=http://localhost:8081/realms/science-alerts-saas \
                        --build-arg VITE_KEYCLOAK_CLIENT_ID=science-alerts-web \
                        -t pubtracker-web:${env.BUILD_NUMBER} web
                """
            }
        }

        stage('Push Docker Images') {
            steps {
                withCredentials([usernamePassword(credentialsId: 'ghcr-token', usernameVariable: 'GHCR_USER', passwordVariable: 'GHCR_TOKEN')]) {
                    sh '''
                        GHCR_USER_LC=$(echo "$GHCR_USER" | tr '[:upper:]' '[:lower:]')
                        echo "$GHCR_TOKEN" | docker login ghcr.io -u "$GHCR_USER" --password-stdin
                        for image in pubtracker-api pubtracker-web; do
                            docker tag $image:''' + env.BUILD_NUMBER + ''' ghcr.io/$GHCR_USER_LC/$image:''' + env.BUILD_NUMBER + '''
                            docker tag $image:''' + env.BUILD_NUMBER + ''' ghcr.io/$GHCR_USER_LC/$image:latest
                            docker push ghcr.io/$GHCR_USER_LC/$image:''' + env.BUILD_NUMBER + '''
                            docker push ghcr.io/$GHCR_USER_LC/$image:latest
                        done
                    '''
                }
            }
        }
    }
}

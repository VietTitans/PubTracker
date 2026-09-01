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

        stage('Build Docker Image') {
            steps {
                // Context is api/ (Dockerfile does `COPY . .` from WORKDIR /src and references
                // RecordService/RecordService.csproj relatively) — matches how docker/docker-compose.yml
                // already builds it (build.context: ../api).
                sh "docker build -f api/Dockerfile -t pubtracker-api:${env.BUILD_NUMBER} api"
            }
        }
    }
}

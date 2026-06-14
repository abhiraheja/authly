---
name: CI/CD — Azure Pipelines + private NuGet + Docker + SSH deploy
date: 2026-04-19
description: Two-stage Azure Pipelines (Build/Push + Deploy) using a runtime-generated nuget.config with a PAT secret, multi-stage .NET Docker image, and password-based SSH to a Dell server running docker compose with a .env-driven image tag.
type: skill-section
---

# CI/CD — Azure Pipelines + private NuGet + Docker + SSH deploy

## When to use

Every Saarvix backend service ships via this exact pipeline shape. **Do not invent a parallel build system.** Variance across services is:

- `SERVICE_NAME`, `IMAGE_NAME`, `ENV_VAR_KEY`, `DOCKERFILE_PATH` — service-specific constants.
- Variable group — environment-specific (`Saarvix-Deploy-Dev-Group` / `...-Stage-Group` / `...-Prod-Group`).

Everything else is copy-paste.

## Architectural decisions

- **Azure Pipelines, not GitHub Actions or Jenkins.** Organization standard; ties into Azure DevOps Artifacts and boards.
- **Multi-stage pipeline:** `Build → Deploy → Cleanup`. Deploy is its own stage gated by `dependsOn: Build` so a failed build never reaches deploy.
- **Image tag = `{env}.{YYYYMMDD}.{seq}`** where `seq = BuildId % 100`. Readable, sortable, unique per day up to 100 builds.
- **`nuget.config` is regenerated at build time** with the PAT injected from the `NUGET_PAT` secret. The committed `nuget.config` has no credentials.
- **Multi-stage Dockerfile** with SDK image for build, aspnet runtime image for the final layer. `USER $APP_UID` in the base layer enforces non-root at runtime.
- **Deploy = SSH + `docker compose up -d`** on a Dell server. `.env` on the server holds the version pin; the pipeline updates it via `sed`, then `docker compose pull && up -d` rolls the single service.
- **`sshpass` password auth** is the current reality (not ideal — a rotating SSH key would be better, but the pattern is not changing without Pakhi's approval).
- **Cleanup stage runs `condition: always()`** so post-build telemetry and notifications fire regardless of pass/fail.

## File layout

```
{repo-root}/
├── azure-pipelines.yml        // the whole pipeline
├── nuget.config               // no credentials; regenerated in CI
├── {ServiceName}.Api/
│   └── Dockerfile             // multi-stage
└── docker-compose.yml         // on the REMOTE server, not the repo
```

## `azure-pipelines.yml` template

```yaml
# ================================================
# Saarvix — {ServiceName}
# Azure DevOps Pipeline
# ================================================

trigger:
  branches:
    include:
      - develop/*
      - main

resources:
  - repo: self

variables:
  - group: Saarvix-Deploy-Dev-Group          # PAT, SSH creds, DOCKER_REGISTRY, REMOTE_PATH, etc.
  - name: SERVICE_NAME
    value: '{service-name}'
  - name: IMAGE_NAME
    value: 'saarvix/{service-name}'
  - name: ENV_VAR_KEY
    value: 'SAARVIX_{SERVICE_NAME_UPPER}_VERSION'
  - name: DOCKERFILE_PATH
    value: '{ServiceName}.Api/Dockerfile'

stages:

  # ================================================
  # STAGE 1 — BUILD & PUSH
  # ================================================
  - stage: Build
    displayName: 'Build & Push'
    jobs:
      - job: BuildAndPush
        pool:
          vmImage: 'ubuntu-latest'
        steps:

          # ---- Generate version tag ----
          - bash: |
              DATE=$(TZ='Asia/Kolkata' date +'%Y%m%d')
              SEQ=$(printf "%02d" $(($(Build.BuildId) % 100)))
              IMAGE_TAG="${BUILD_ENV}.${DATE}.${SEQ}"
              echo "##vso[task.setvariable variable=IMAGE_TAG;isOutput=true]${IMAGE_TAG}"
              echo "##vso[build.updatebuildnumber]${IMAGE_TAG}"
            name: versioning

          # ---- Inject PAT into nuget.config ----
          - bash: |
              cat > $(Build.SourcesDirectory)/nuget.config << EOF
              <?xml version="1.0" encoding="utf-8"?>
              <configuration>
                <packageSources>
                  <clear />
                  <add key="SaarvixPackages" value="https://pkgs.dev.azure.com/avdesignworks/Saarvix/_packaging/SaarvixPackages/nuget/v3/index.json" />
                  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                </packageSources>
                <packageSourceCredentials>
                  <SaarvixPackages>
                    <add key="Username" value="docker" />
                    <add key="ClearTextPassword" value="${NUGET_PAT}" />
                  </SaarvixPackages>
                </packageSourceCredentials>
              </configuration>
              EOF
            env:
              NUGET_PAT: $(NUGET_PAT)

          # ---- Docker Build ----
          - bash: |
              echo "$DOCKER_PASS" | docker login $DOCKER_REGISTRY \
                -u "$DOCKER_USER" --password-stdin

              docker build \
                -t $DOCKER_REGISTRY/$(IMAGE_NAME):$(versioning.IMAGE_TAG) \
                -f $(DOCKERFILE_PATH) \
                $(Build.SourcesDirectory)
            env:
              DOCKER_USER: $(DOCKER_USER)
              DOCKER_PASS: $(DOCKER_PASS)

          # ---- Docker Push ----
          - bash: |
              docker push $DOCKER_REGISTRY/$(IMAGE_NAME):$(versioning.IMAGE_TAG)
              docker logout $DOCKER_REGISTRY
            env:
              DOCKER_USER: $(DOCKER_USER)
              DOCKER_PASS: $(DOCKER_PASS)

  # ================================================
  # STAGE 2 — DEPLOY
  # ================================================
  - stage: Deploy
    dependsOn: Build
    condition: succeeded()
    variables:
      IMAGE_TAG: $[ stageDependencies.Build.BuildAndPush.outputs['versioning.IMAGE_TAG'] ]
    jobs:
      - deployment: DeployToServer
        environment: 'saarvix-dev'
        pool:
          vmImage: 'ubuntu-latest'
        strategy:
          runOnce:
            deploy:
              steps:
                - bash: |
                    sudo apt-get install -y sshpass
                    sshpass -p "$SSH_PASS" ssh \
                      -p "$SSH_PORT" \
                      -o StrictHostKeyChecking=no \
                      "$SSH_USER@$SSH_HOST" \
                      "IMAGE_TAG='$(IMAGE_TAG)' \
                       DOCKER_USER='$DOCKER_USER' \
                       DOCKER_PASS='$DOCKER_PASS' \
                       SERVICE_NAME='$(SERVICE_NAME)' \
                       ENV_VAR_KEY='$(ENV_VAR_KEY)' \
                       DOCKER_REGISTRY='$(DOCKER_REGISTRY_LOCAL)' \
                       REMOTE_PATH='$(REMOTE_PATH)' \
                       REMOTE_ENV_PATH='$(REMOTE_ENV_PATH)' \
                       bash -s" << 'REMOTE_SCRIPT'

                    set -euo pipefail

                    # Update / insert the version pin in the shared .env
                    if grep -q "^${ENV_VAR_KEY}=" "$REMOTE_ENV_PATH"; then
                      sed -i "s|^${ENV_VAR_KEY}=.*|${ENV_VAR_KEY}=${IMAGE_TAG}|" "$REMOTE_ENV_PATH"
                    else
                      echo "${ENV_VAR_KEY}=${IMAGE_TAG}" >> "$REMOTE_ENV_PATH"
                    fi

                    echo "$DOCKER_PASS" | docker login $DOCKER_REGISTRY \
                      -u "$DOCKER_USER" --password-stdin

                    cd "$REMOTE_PATH"
                    docker compose pull $SERVICE_NAME
                    docker compose up -d $SERVICE_NAME

                    docker logout $DOCKER_REGISTRY
                    REMOTE_SCRIPT
                  env:
                    SSH_PASS: $(SSH_PASS)
                    SSH_HOST: $(SSH_HOST)
                    SSH_PORT: $(SSH_PORT)
                    SSH_USER: $(SSH_USER)
                    DOCKER_USER: $(DOCKER_USER)
                    DOCKER_PASS: $(DOCKER_PASS)

  # ================================================
  # CLEANUP
  # ================================================
  - stage: Cleanup
    dependsOn:
      - Build
      - Deploy
    condition: always()
    jobs:
      - job: Cleanup
        pool: { vmImage: 'ubuntu-latest' }
        steps:
          - bash: echo "Pipeline completed: $(Build.BuildNumber)"
```

## Variable group conventions

The `Saarvix-Deploy-{Env}-Group` holds all secrets and environment-specific values. **Do not** hardcode any of these in the YAML:

| Variable | Purpose |
|---|---|
| `BUILD_ENV` | `dev` / `stage` / `prod` — embedded in the image tag |
| `NUGET_PAT` | Azure DevOps PAT with `Packaging (Read)` scope |
| `DOCKER_USER`, `DOCKER_PASS`, `DOCKER_REGISTRY` | Registry credentials (CI-side) |
| `DOCKER_REGISTRY_LOCAL` | Registry hostname as seen from the remote server (may differ if internal DNS) |
| `SSH_HOST`, `SSH_PORT`, `SSH_USER`, `SSH_PASS` | Deploy target |
| `REMOTE_PATH` | Dir on remote containing `docker-compose.yml` |
| `REMOTE_ENV_PATH` | Absolute path to the shared `.env` on remote |

Mark `NUGET_PAT`, `DOCKER_PASS`, `SSH_PASS` as secret in the Variable Group. Non-secret vars can be plain.

## Multi-stage Dockerfile template

```dockerfile
# See https://aka.ms/customizecontainer for VS fast-mode context.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID                              # non-root by default
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src

# Copy ONLY csproj + nuget.config first for a cached restore layer
COPY ["nuget.config", "."]
COPY ["{ServiceName}.Api/{ServiceName}.Api.csproj",                     "{ServiceName}.Api/"]
COPY ["{ServiceName}.AzureServiceBus/{ServiceName}.AzureServiceBus.csproj", "{ServiceName}.AzureServiceBus/"]
COPY ["{ServiceName}.Infrastructure/{ServiceName}.Infrastructure.csproj", "{ServiceName}.Infrastructure/"]
COPY ["{ServiceName}.Application/{ServiceName}.Application.csproj",     "{ServiceName}.Application/"]
COPY ["{ServiceName}.Domain/{ServiceName}.Domain.csproj",               "{ServiceName}.Domain/"]
COPY ["{ServiceName}.Models/{ServiceName}.Models.csproj",               "{ServiceName}.Models/"]
RUN dotnet restore "./{ServiceName}.Api/{ServiceName}.Api.csproj"

COPY . .
WORKDIR "/src/{ServiceName}.Api"
RUN dotnet build "./{ServiceName}.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./{ServiceName}.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "{ServiceName}.Api.dll"]
```

**Why copy csproj before source?** Docker layer caching: if source changes but csprojs don't, the `dotnet restore` layer is cached and the build skips NuGet entirely.

**Why `/p:UseAppHost=false`?** We don't need the native executable wrapper — we invoke `dotnet {Service}.dll` directly. Saves ~20MB per image.

## Remote `docker-compose.yml` (on the Dell server, not in repo)

Pinned to an env var so the pipeline can rotate it by editing `.env`:

```yaml
services:
  saar-wallet:
    image: ${DOCKER_REGISTRY}/saarvix/saar-wallet:${SAARVIX_SAAR_WALLET_VERSION}
    restart: unless-stopped
    env_file: .env
    ports:
      - "8081:8080"
    depends_on:
      - mongodb
```

## Secret hygiene

- **Never log `NUGET_PAT`, `DOCKER_PASS`, `SSH_PASS`.** Azure Pipelines auto-masks them in output, but a `set -x` or `cat nuget.config` will bypass masking by printing chunks.
- **The generated `nuget.config` in CI contains a PAT in cleartext.** Ensure artifact retention does not publish it; the file lives only on the agent VM.
- **SSH password auth** is brittle — push for rotation to SSH keys in the next iteration. Keys add ~10 minutes of setup and remove a class of incidents.

## Common mistakes

1. **Committing `nuget.config` with credentials.** Treat it like `.env` — the committed version must be credential-free.
2. **Image tag without env prefix.** `20260419.05` is ambiguous across dev/stage/prod; `dev.20260419.05` is greppable.
3. **Using `:latest` tag.** Breaks reproducibility, breaks rollback. Pipeline must always produce an immutable tag.
4. **Forgetting `-o StrictHostKeyChecking=no` on first deploy.** SSH hangs waiting for confirmation; pipeline times out silently.
5. **`docker compose up -d` without `pull` first.** Uses the cached local image and silently skips the new version.
6. **Editing the remote `.env` concurrently from multiple pipelines.** Two services deploying at once can `sed` each other. Use `flock` on the `.env` if this becomes an issue.
7. **Running `docker compose down` instead of `up -d`.** `down` removes networks and volumes — data loss on anything with a named volume.

## Local replication

To reproduce a CI build locally (useful for debugging cache misses):

```bash
# Paste your PAT into nuget.config manually (DO NOT commit)
docker build -t saar-wallet:local -f {ServiceName}.Api/Dockerfile .
docker run --rm -p 8080:8080 --env-file .env.local saar-wallet:local
```

## Rollback procedure

If a deploy breaks prod:

```bash
# On the remote server:
cd $REMOTE_PATH
sed -i "s|^SAARVIX_SAAR_WALLET_VERSION=.*|SAARVIX_SAAR_WALLET_VERSION=<previous-tag>|" .env
docker compose pull saar-wallet
docker compose up -d saar-wallet
```

No re-run of CI is needed — the old tag is still in the registry.

## Related skills

- `13-shared-saar-packages.md` — why the PAT is needed for `dotnet restore`.
- `01-solution-layout-and-reference-chain.md` — explains the csproj copy order in the Dockerfile.
- `10-azure-keyvault-and-configuration.md` — why most secrets flow through Key Vault at runtime, not the `.env` on the remote.

# Repository structure

AIWorkHub keeps each service's projects together without repeated source-directory nesting.

```text
AIWorkHub/
  Services/
    IdentityTenant/
      IdentityTenant.Api/
      IdentityTenant.Application/
      IdentityTenant.Domain/
      IdentityTenant.Infrastructure/
      IdentityTenant.Contracts/
      IdentityTenant.Tests/
      Database/
    Jira/
    Audit/
    AIAgent/
    Persona/
  Gateway/
  Shared/
  Frontend/
    apps/
    packages/shared/src/
    tests/
  Tests/integration/
  Infrastructure/
  Scripts/
  Docs/
  AIWorkHub.sln
```

Existing .NET project and namespace names are unchanged, including the `.Api` suffix. Controllers and HTTP configuration belong in the API project; business logic belongs in Application; entities belong in Domain; persistence belongs in Infrastructure; transport contracts belong in Contracts.

Each service's `<Service>.Tests/` folder contains its existing test projects. Cross-service tests live under `Tests/integration/`, and gateway tests remain with the gateway. AIAgent evaluations live in `Services/AIAgent/AIAgent.Evals/`. Only implemented folders are included; Persona does not yet have a separate service-test project or owned database folder.

Service-owned database assets live under `Services/<Service>/Database/`. Shared initialization and assets without a service owner live under `Infrastructure/database/`. Employee schemas retain their existing IdentityTenant database ownership. SQL ordering and filenames are unchanged.

Frontend apps remain npm workspaces. Shared source stays under `Frontend/packages/shared/src/`, with browser tests in `Frontend/tests/` so dependencies resolve within the same workspace.

`Directory.Build.props`, `Directory.Packages.props`, and `global.json` provide shared build settings, dependency versions, and the SDK selection. Docker Compose lives under `Infrastructure/docker/`; the root Dockerfile remains the cloud deployment entry point.

Generated logs, build outputs, dependencies, and personal IDE settings stay out of version control. Add runbooks, architecture decisions, and new infrastructure tools when needed rather than creating empty placeholders.

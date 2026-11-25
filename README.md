# AIWorkHub

AIWorkHub is organized as a .NET backend with independently bounded services, a
separate API gateway, shared building blocks, database assets, and deployment
configuration.

## Repository structure

```text
AIWorkHub.sln
├── backend/
│   ├── services/
│   │   ├── AIAgent/
│   │   │   ├── AIAgent.Api/
│   │   │   ├── AIAgent.Application/
│   │   │   ├── AIAgent.Contracts/
│   │   │   ├── AIAgent.Domain/
│   │   │   ├── AIAgent.Infrastructure/
│   │   │   ├── tests/
│   │   │   └── evals/
│   │   ├── Audit/
│   │   │   ├── Audit.Api/
│   │   │   ├── Audit.Application/
│   │   │   ├── Audit.Contracts/
│   │   │   ├── Audit.Domain/
│   │   │   ├── Audit.Infrastructure/
│   │   │   └── tests/
│   │   ├── IdentityTenant/
│   │   │   ├── IdentityTenant.Api/
│   │   │   ├── IdentityTenant.Application/
│   │   │   ├── IdentityTenant.Contracts/
│   │   │   ├── IdentityTenant.Domain/
│   │   │   ├── IdentityTenant.Infrastructure/
│   │   │   └── tests/
│   │   └── Jira/
│   │       ├── Jira.Api/
│   │       │   └── Integrations/IdentityTenant/
│   │       ├── Jira.Application/
│   │       ├── Jira.Contracts/
│   │       ├── Jira.Domain/
│   │       ├── Jira.Infrastructure/
│   │       └── tests/
│   ├── gateway/
│   │   ├── ApiGateway/
│   │   └── ApiGateway.Tests/
│   ├── building-blocks/
│   ├── database/
│   └── tests/
│       └── Backend.Authorization.Tests/
└── infrastructure/
    └── docker/
```

Each service retains its own API, application, domain, infrastructure, and
contracts projects where those layers exist. HTTP controllers and API-specific
services stay in the API project; business logic and data access stay in their
respective layers. Shared concerns are implemented in `building-blocks`, and
cross-service authorization tests remain in `backend/tests`.

The API Gateway is separate from the services. Database ownership and scripts
remain under `backend/database`; Docker Compose configuration remains under
`infrastructure/docker`.

`AIWorkHub.sln` is the single solution for the repository. It includes the
implemented services, shared projects, gateway, and tests.

## Authentication

IdentityTenant is the single user sign-in and session service. It handles
Google/password login and issues AIWorkHub access tokens. Jira, Audit, and
AIAgent do not implement separate user login flows; they validate incoming
bearer tokens using the shared `backend/building-blocks/Authentication`
library. Services do not call IdentityTenant for every request just to verify a
token. Jira's `Integrations/IdentityTenant` adapters call IdentityTenant
directory endpoints when Jira needs user or approver details; they are service
integrations, not authentication providers. AIAgent also calls IdentityTenant
for business operations that need directory data, but derives the current
caller from the locally validated token.

Configure the same JWT signing key in IdentityTenant and each token-validating
service through secret storage or `Jwt__SigningKey`; do not commit the key.
AIAgent's local development issuer and audience are in
`backend/services/AIAgent/AIAgent.Api/appsettings.Development.json`.

## Build and test

From the repository root, build the solution:

```sh
dotnet build AIWorkHub.sln
```

Run its tests with:

```sh
dotnet test AIWorkHub.sln
```

Some integration tests require Docker to be running. PostgreSQL setup and reset
guidance is in [`backend/database/postgres/README.md`](backend/database/postgres/README.md).

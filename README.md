# AIWorkHub

AIWorkHub is organized as a .NET backend with independently bounded services, a
separate API gateway, shared building blocks, database assets, and deployment
configuration.

## Repository structure

- `Services/`: service implementation, service tests, and owned database assets.
- `Frontend/apps/`: five portal applications.
- `Frontend/packages/shared/`: shared frontend source.
- `Gateway/` and `Shared/`: API gateway and technical libraries.
- `Tests/integration/`: cross-service backend tests.
- `Infrastructure/`: Docker Compose and shared database provisioning.
- `Scripts/`: development and database operations.
- `Docs/`: architecture, development guidance, and test plans.

See [structure and ownership](Docs/architecture/repository-structure.md),
[local development](Docs/development/getting-started.md), and
[contributing](CONTRIBUTING.md).

`AIWorkHub.sln` includes all backend projects. Shared build settings and package
versions live in `Directory.Build.props` and `Directory.Packages.props`.

## Authentication

IdentityTenant is the single user sign-in and session service. It handles
Google/password login and issues AIWorkHub access tokens. Jira, Audit, and
AIAgent do not implement separate user login flows; they validate incoming
bearer tokens using the shared `Shared/Authentication`
library. Services do not call IdentityTenant for every request just to verify a
token. Jira's `Integrations/IdentityTenant` adapters call IdentityTenant
directory endpoints when Jira needs user or approver details; they are service
integrations, not authentication providers. AIAgent also calls IdentityTenant
for business operations that need directory data, but derives the current
caller from the locally validated token.

Configure the same JWT signing key in IdentityTenant and each token-validating
service through secret storage or `Jwt__SigningKey`; do not commit the key.
AIAgent's local development issuer and audience are in
`Services/AIAgent/AIAgent.Api/appsettings.Development.json`.

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
guidance is in [`Infrastructure/database/postgres/README.md`](Infrastructure/database/postgres/README.md).

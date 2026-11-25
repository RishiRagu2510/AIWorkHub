# Local development

Run commands from the repository root unless noted otherwise. Install the .NET SDK selected by `global.json`, Node.js 22, npm, and Docker for database integration tests.

```sh
dotnet restore AIWorkHub.sln
dotnet build AIWorkHub.sln
dotnet test AIWorkHub.sln
npm ci --prefix Frontend
npm run build --prefix Frontend
npm run dev:all --prefix Frontend
```

Start an individual service with its project path, for example:

```sh
dotnet run --project Services/IdentityTenant/IdentityTenant.Api
```

Configure service credentials using .NET user secrets or environment variables. Existing UserSecretsId values are preserved. Copy `Frontend/.env.example` to `.env.local` in that directory to configure the frontend API target. The frontend development server uses HTTPS when local certificates are present in `Frontend/.certs`.

Copy the root `.env.example` to `.env` and supply the PostgreSQL, MongoDB, Redis, and MinIO environment values referenced by `Infrastructure/docker/docker-compose.yml` before starting infrastructure:

```sh
docker compose -f Infrastructure/docker/docker-compose.yml up -d
```

See [database setup](../../Infrastructure/database/postgres/README.md) for schema commands. The reset script is destructive and is never part of setup or CI.

Run browser tests from `Frontend` with `npm test`. They require running backend services, configured test accounts, and the HTTPS portal URLs currently specified in Playwright configuration. CI builds all portals and checks browser-test discovery; it does not provision authenticated browser-test environments.

After switching from the previous layout, reopen `AIWorkHub.sln`, update IDE run configurations or external deployment working directories, and run `npm ci --prefix Frontend` to recreate workspace links.

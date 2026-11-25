# Contributing

See [repository structure](Docs/architecture/repository-structure.md) for placement and ownership boundaries and [development](Docs/development/getting-started.md) for commands.

Keep each change focused. Include its behavior, validation results, and deployment or migration requirements in the pull request. Request review from maintainers of the affected services. Repository administrators should configure CODEOWNERS with real organization teams and branch protection when those teams are assigned.

Place service projects directly under `Services/<Service>/` and service test projects under its `<Service>.Tests/` directory. Keep business rules in the owning service; shared building blocks contain technical concerns. Put cross-service backend tests in `Tests/integration/` and browser tests in the frontend workspace.

Add NuGet versions to `Directory.Packages.props`. Existing `VersionOverride` entries preserve previously different project versions; changing those versions is a separate dependency update. Use the frontend npm workspace and commit its lockfile when dependencies change.

Never commit credentials, generated outputs, local certificates, or database dumps. Use .NET user secrets or environment variables for secrets. Run the relevant build and tests before requesting review.

# ==========================================
# Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and backend source code
COPY AIWorkHub.sln .
COPY Services/ Services/
COPY Gateway/ Gateway/
COPY Shared/ Shared/
COPY Tests/ Tests/
COPY Directory.Build.props Directory.Packages.props global.json ./

# Publish all required microservices
RUN dotnet publish Gateway/ApiGateway/ApiGateway.csproj -c Release -o /app/gateway
RUN dotnet publish Services/IdentityTenant/IdentityTenant.Api/IdentityTenant.Api.csproj -c Release -o /app/identity
RUN dotnet publish Services/Jira/Jira.Api/Jira.Api.csproj -c Release -o /app/jira
RUN dotnet publish Services/Persona/Persona.Api/Persona.Api.csproj -c Release -o /app/persona
RUN dotnet publish Services/Audit/Audit.Api/Audit.Api.csproj -c Release -o /app/audit
RUN dotnet publish Services/AIAgent/AIAgent.Api/AIAgent.Api.csproj -c Release -o /app/aiagent

# ==========================================
# Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published outputs from build stage
COPY --from=build /app /app

# Copy entrypoint script
COPY Scripts/development/start.sh /app/start.sh
RUN chmod +x /app/start.sh

# Render and cloud platforms expose PORT environment variable
ENV PORT=5204
EXPOSE 5204

ENTRYPOINT ["/app/start.sh"]

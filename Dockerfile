# ==========================================
# Build Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and backend source code
COPY AIWorkHub.sln .
COPY backend/ backend/

# Publish all required microservices
RUN dotnet publish backend/gateway/ApiGateway/ApiGateway.csproj -c Release -o /app/gateway
RUN dotnet publish backend/services/IdentityTenant/IdentityTenant.Api/IdentityTenant.Api.csproj -c Release -o /app/identity
RUN dotnet publish backend/services/Jira/Jira.Api/Jira.Api.csproj -c Release -o /app/jira
RUN dotnet publish backend/services/Persona/Persona.Api/Persona.Api.csproj -c Release -o /app/persona
RUN dotnet publish backend/services/Audit/Audit.Api/Audit.Api.csproj -c Release -o /app/audit
RUN dotnet publish backend/services/AIAgent/AIAgent.Api/AIAgent.Api.csproj -c Release -o /app/aiagent

# ==========================================
# Runtime Stage
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Copy published outputs from build stage
COPY --from=build /app /app

# Copy entrypoint script
COPY start.sh /app/start.sh
RUN chmod +x /app/start.sh

# Render and cloud platforms expose PORT environment variable
ENV PORT=5204
EXPOSE 5204

ENTRYPOINT ["/app/start.sh"]

#!/bin/sh
set -e

GATEWAY_PORT="${PORT:-5204}"

echo "Starting AIWorkHub microservices..."

ASPNETCORE_URLS="http://127.0.0.1:5233" dotnet /app/identity/IdentityTenant.Api.dll &
ASPNETCORE_URLS="http://127.0.0.1:5148" dotnet /app/jira/Jira.Api.dll &
ASPNETCORE_URLS="http://127.0.0.1:5234" dotnet /app/persona/Persona.Api.dll &
ASPNETCORE_URLS="http://127.0.0.1:5067" dotnet /app/audit/Audit.Api.dll &
ASPNETCORE_URLS="http://127.0.0.1:5250" dotnet /app/aiagent/AIAgent.Api.dll &

echo "Starting ApiGateway on port ${GATEWAY_PORT}..."
export ASPNETCORE_URLS="http://0.0.0.0:${GATEWAY_PORT}"
exec dotnet /app/gateway/ApiGateway.dll

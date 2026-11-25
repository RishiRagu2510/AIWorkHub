#!/bin/sh
set -e

mkdir -p /tmp/keys

GATEWAY_PORT="${PORT:-5204}"

echo "Starting AIWorkHub microservices..."

(cd /app/identity && ASPNETCORE_URLS="http://127.0.0.1:5233" dotnet IdentityTenant.Api.dll) &
(cd /app/jira && ASPNETCORE_URLS="http://127.0.0.1:5148" dotnet Jira.Api.dll) &
(cd /app/persona && ASPNETCORE_URLS="http://127.0.0.1:5234" dotnet Persona.Api.dll) &
(cd /app/audit && ASPNETCORE_URLS="http://127.0.0.1:5067" dotnet Audit.Api.dll) &
(cd /app/aiagent && ASPNETCORE_URLS="http://127.0.0.1:5250" dotnet AIAgent.Api.dll) &

echo "Starting ApiGateway on port ${GATEWAY_PORT}..."
cd /app/gateway
export ASPNETCORE_URLS="http://0.0.0.0:${GATEWAY_PORT}"
exec dotnet ApiGateway.dll

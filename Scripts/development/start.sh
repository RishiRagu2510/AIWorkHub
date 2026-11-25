#!/bin/sh
set -e

mkdir -p /tmp/keys

# Disable Kafka if not explicitly configured
export Kafka__BootstrapServers="${Kafka__BootstrapServers:-disabled}"

# If DATABASE_URL or SUPABASE_DB_URL is provided, map to each service's connection string
DB_CONN="${DATABASE_URL:-$SUPABASE_DB_URL}"
if [ -n "$DB_CONN" ]; then
  echo "Configuring PostgreSQL connection strings from environment..."
  export ConnectionStrings__IdentityTenant="${ConnectionStrings__IdentityTenant:-$DB_CONN}"
  export ConnectionStrings__Jira="${ConnectionStrings__Jira:-$DB_CONN}"
  export ConnectionStrings__Audit="${ConnectionStrings__Audit:-$DB_CONN}"
  export ConnectionStrings__Persona="${ConnectionStrings__Persona:-$DB_CONN}"
  export KnowledgeBase__ConnectionString="${KnowledgeBase__ConnectionString:-$DB_CONN}"
fi

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

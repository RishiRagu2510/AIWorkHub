# AIWorkHub MinIO Buckets

AIWorkHub uses MinIO as S3-compatible object storage.

## Buckets

- attachments
- documents
- build-artifacts
- test-reports
- ai-artifacts
- bug-fix-artifacts

## Object key convention

Tenant-owned objects must include the tenant ID in the object key.

Examples:

attachments/tenant-1/issues/101/error-log.txt

documents/tenant-1/pages/42/design.md

build-artifacts/tenant-1/repositories/12/runs/55/build.zip

test-reports/tenant-1/repositories/12/runs/55/test-results.xml

ai-artifacts/tenant-1/runs/200/output.json

bug-fix-artifacts/tenant-1/jobs/300/logs/test-output.txt

## Security

All buckets are private.

Applications access objects through the backend services rather than exposing
MinIO directly to end users.
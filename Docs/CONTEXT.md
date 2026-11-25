# Context
Business and product context for the application under test; target state only.

## Application
AIWorkHub is an enterprise work management platform organized as a .NET backend with independently bounded microservices and multiple frontend portals.
Frontend portals include:
- GridHub (Workspace & Main Work Management Portal, default port 5173)
- Hub (Portal Switcher, port 5172)
- Landing (Public Landing Page, port 5171)
- Persona (Employee Directory and Profile Portal, port 5174)
- Chronos (Timesheet & Time Tracking Portal, port 5175)

## Modules
- GridHub (Workspace): Projects, issues, and team activity management.
- Authentication & Session: IdentityTenant service providing single sign-on, JWT token issuance, email/password login, and Google SSO.
- AIAgent: Intelligent agent service providing assisted workflows.
- Jira Integration: Jira issue synchronization, approver resolution, and directory adapters.
- Chronos: Timesheet recording and tracking.
- Persona: Employee directory and user profiles.

## Environments
- Local Frontend (GridHub): http://localhost:5173
- Local Landing: http://localhost:5171
- Local Hub: http://localhost:5172
- Local Persona: http://localhost:5174
- Local Chronos: http://localhost:5175
- Local Backend API Gateway: http://localhost:5204 (configured target in vite.shared.js)
- Test/Non-Production: Configured via .env.development / .env.production

## Roles and Test Accounts (kinds only)
- Organization Employee (standard user with email/password or Google SSO)
- Approver / Manager (inferred from Jira Integration directory requirements)
- Organization Administrator (inferred)

## Business Rules (confirmed)
- Password sign-in validates required inputs (email up to 320 chars, password up to 128 chars) and presents accessible role="alert" message upon authentication failure — PasswordSignIn.jsx & live UI observation
- Google SSO loads Google Identity Services, fetches client configuration and CSRF token from /api/auth/google/configuration, and renders "Continue with Google" iframe button — GoogleSignIn.jsx & live UI observation
- IdentityTenant is the single user sign-in and session service; issues AIWorkHub access tokens — README.md §Authentication
- Jira, Audit, and AIAgent validate bearer tokens using shared Authentication building block without calling IdentityTenant on every request — README.md §Authentication
- Login page allows email/password sign-in and Google SSO authentication — Frontend/tests/e2e/auth.spec.ts & live UI observation
- Unauthenticated access to the workspace root routes to /login — live UI observation
- Login page provides expandable trouble signing in guidance — Frontend/tests/e2e/auth.spec.ts & live UI observation

## External Systems
- Google OAuth (SSO)
- PostgreSQL (database backend)
- Kafka (event streaming)
- MinIO (object storage)

## Glossary
- GridHub: Main workspace web application for projects and issues.
- IdentityTenant: Authentication and tenant management microservice.
- Building Blocks: Shared class libraries and cross-cutting concerns in backend.

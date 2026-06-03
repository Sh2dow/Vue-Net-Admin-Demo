# Current Deployment Plan

## Completed
1. [DONE] Deploy infrastructure via Bicep (ACR, Container Apps, PG, Key Vault, Service Bus)
2. [DONE] Fix crash bugs: auth-api SeedData, tasks-api/payments-api Program.cs
3. [DONE] Fix PostgreSQL TCP transport (Auto→Tcp)
4. [DONE] Create all 4 PostgreSQL databases
5. [DONE] Apply DB migrations and seed data
6. [DONE] Fix frontend nginx.conf (removed proxy_pass to non-existent backend_api)
7. [DONE] Fix frontend Dockerfile for Vite build-time env vars (.env.production approach)
8. [DONE] Deploy frontend v3 with correct build-time env vars — verified api-gateway URL is baked in
9. [DONE] Git commit all infrastructure and code fixes (commit `efa8cb7`)

## Remaining
10. [TODO] E2E authentication test via API Gateway (blocked by HTTPS forwarding issue)
11. [TODO] Configure ForwardedHeaders middleware in auth-api to fix HTTPS requirement
12. [TODO] Test frontend login flow end-to-end

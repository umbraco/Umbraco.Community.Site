---
tags: [backoffice, api, authorization, swagger]
---

# Secured backoffice Management API endpoints

> **Status:** Planned — this page is a stub. The full tutorial hasn't been written yet; see the [tutorial backlog](../IDEAS.md) for the framing and motivation.

Community content on the new Umbraco backoffice almost exclusively covers the UI side — property editors, dashboards, workspace views. The C# endpoints those UIs actually call are consistently under-documented. The `BlockRestrictionApiController` in this repo is a clean example of the secured backend half: routing under `/umbraco/.../api/v1`, locking endpoints down with `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]`, Swagger doc registration so the endpoints show up in the API docs, and a typed fetch wrapper on the client that pulls the user's backoffice bearer token automatically.

## What this will cover

- Routing a controller under the backoffice scope.
- Locking endpoints down with `AuthorizationPolicies.SectionAccessContent`.
- Registering Swagger documentation for backoffice APIs.
- A typed client-side fetch wrapper that handles backoffice auth for you.

*If you're picking this up to write, follow the section structure in [Contributing a new tutorial](../README.md#contributing-a-new-tutorial).*

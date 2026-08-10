---
tags: [backoffice, api, authorization, swagger]
---

# How to secure a custom backoffice Management API endpoint

Community content on the new Umbraco backoffice almost exclusively covers the UI side — property editors, dashboards, workspace views. The C# endpoints those UIs actually call are consistently under-documented. This tutorial is the other half: how `BlockRestrictionApiController` — and its three near-identical siblings elsewhere in this repo — routes under the backoffice, locks itself down to users with the right section access, registers itself in the API docs, and gets called from a typed client. It's a *foundation* piece, and a direct sequel to the [backoffice extensions primer](../../primers/backoffice.md#talking-to-the-c-apis), which sketches the client side in a few paragraphs and explicitly defers the backend half to here.

## Why you might want this

The moment a property editor, dashboard, or workspace view needs data Umbraco's own Management API has no concept of — block restriction rules, 404 hit logs, a Discord announcement run history — you're writing a custom controller. That controller needs to answer three questions before it's safe to ship: who's allowed to call it, how does a backoffice API client authenticate against it, and how does it show up in the API docs so the next contributor can find it without reading your source. Umbraco already has good, consistent answers to all three; the trick is knowing where they live so you don't reinvent them.

## What we're building

One **base controller** per package, carrying every cross-cutting concern as attributes, and one **concrete controller** per package carrying only actions:

```csharp
[ApiController]
[BackOfficeRoute("umbracocommunityblockrestrictions/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
[ApiVersion("1.0")]
public abstract class BlockRestrictionApiControllerBase : ControllerBase
{
}
```

Four attributes, four jobs: route prefix, authorization policy, API-doc grouping, and version. Everything below is what each one actually does, and the composer registration that ties `[MapToApi]` to a real, browsable API document.

## Why the obvious fix doesn't work

**Rolling your own auth check.** It's tempting to write a custom `[Authorize]` policy, or worse, an ad-hoc header check, that verifies "is this a signed-in backoffice user." Umbraco already ships around thirty section/tree-access policies as plain string constants on `AuthorizationPolicies` (`Umbraco.Cms.Web.Common.Authorization`) — `SectionAccessContent` among them — each one requiring the backoffice's own OpenIddict bearer-token scheme *and* checking the resolved `IUser`'s `AllowedSections` against the policy's allowed values. Writing your own version means re-solving OAuth token validation and user-permission resolution Umbraco already gets right. Every custom API controller in this repo — Block Restrictions, NotFoundTracker, Blog Announcements, Extensions — uses the exact same `SectionAccessContent` policy. That's convention, not coincidence.

**Following an older Umbraco Swagger tutorial.** Search for "Umbraco custom Swagger document" and a lot of what comes back references `Configure<SwaggerGenOptions>`, `opt.SwaggerDoc(...)`, and a custom `OperationFilter` deriving from `BackOfficeSecurityRequirementsOperationFilterBase`. None of those types exist anymore as of Umbraco 18 (the version this repo runs) — Umbraco dropped Swashbuckle's document generation in favour of .NET's own `Microsoft.AspNetCore.OpenApi`, keeping Swashbuckle only for the browsable UI. This repo's own git history has the migration commit, and it's worth reading as the real "why" here rather than taking either the old or new pattern on faith.

**Hardcoding the route prefix.** Writing `[Route("/umbraco/mypackage/api/v1/...")]` directly works today and breaks the moment the backoffice path is reconfigured, or you need a `v2`. `[BackOfficeRoute]` and the `{version:apiVersion}` token exist so neither of those is your problem to solve by hand.

## Walkthrough

### Step 1 — One base controller, every cross-cutting attribute

[`BlockRestrictionApiControllerBase.cs`](../../../src/UmbracoCommunity.BlockRestrictions/Controllers/BlockRestrictionApiControllerBase.cs) — the whole file, because there's nothing else in it:

```csharp
[ApiController]
[BackOfficeRoute("umbracocommunityblockrestrictions/api/v{version:apiVersion}")]
[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]
[MapToApi(Constants.ApiName)]
[ApiVersion("1.0")]
public abstract class BlockRestrictionApiControllerBase : ControllerBase
{
}
```

Base class is plain `ControllerBase` — not an Umbraco-specific base. `[BackOfficeRoute("umbracocommunityblockrestrictions/api/v{version:apiVersion}")]` prepends a `[umbracoBackOffice]` route token Umbraco resolves at startup to the configured backoffice path (`umbraco` by default), so the resolved prefix is `/umbraco/umbracocommunityblockrestrictions/api/v{version}`. `{version:apiVersion}` is filled in by `[ApiVersion("1.0")]` (from the `Asp.Versioning` package) — the literal URL ends up `/umbraco/umbracocommunityblockrestrictions/api/v1/...`, but nothing in this stack hardcodes the `1`.

Every concrete controller in the package inherits this and adds nothing but actions — `BlockRestrictionApiController` itself carries zero routing or auth attributes of its own.

### Step 2 — `SectionAccessContent` is Umbraco's policy, not this repo's

[`AuthorizationPolicies.SectionAccessContent`](https://docs.umbraco.com/umbraco-cms/extend-your-project/tutorials/creating-a-backoffice-api/access-policies) is a constant Umbraco itself defines and registers — this repo only references it. Umbraco registers roughly thirty of these as part of `.AddBackOffice()` in `Program.cs`, each one built the same way:

```csharp
// Inside Umbraco's own AddAuthorizationPolicies (not this repo's code)
void AddAllowedApplicationsPolicy(string policyName, params string[] allowedClaimValues)
{
    options.AddPolicy(policyName, policy =>
    {
        policy.AuthenticationSchemes.Add("OpenIddict.Validation.AspNetCore");
        policy.Requirements.Add(new AllowedApplicationRequirement(allowedClaimValues));
    });
}
// ...
AddAllowedApplicationsPolicy("SectionAccessContent", "content");
```

The requirement it adds is checked by a handler that resolves the current backoffice user and asks whether their `AllowedSections` (their user-group-derived permissions) contains `"content"`:

```csharp
// Also Umbraco's own code
protected override Task<bool> IsAuthorized(AuthorizationHandlerContext context, AllowedApplicationRequirement requirement)
    => Task.FromResult(_authorizationHelper.TryGetUmbracoUser(context.User, out var user)
        && user.AllowedSections.ContainsAny(requirement.Applications));
```

So `[Authorize(Policy = AuthorizationPolicies.SectionAccessContent)]` means: reject anything not carrying a valid backoffice OAuth2 bearer token (the `OpenIddict.Validation.AspNetCore` scheme requirement), *and* reject any authenticated user whose backoffice permissions don't include Content-section access. Both checks, one attribute, none of it this repo's to implement or maintain.

### Step 3 — Register an OpenAPI document, and mean OpenAPI

[`BlockRestrictionComposer.cs`](../../../src/UmbracoCommunity.BlockRestrictions/BlockRestrictionComposer.cs):

```csharp
// WithBackOfficeAuthentication() wires up the Umbraco backoffice "Authorize" button
// in the docs UI, so you can test the API endpoints while authenticated.
builder.AddBackOfficeOpenApiDocument(Constants.ApiName, doc => doc
    .WithTitle("Umbraco Community Block Restrictions Backoffice API")
    .WithBackOfficeAuthentication());
```

[`AddBackOfficeOpenApiDocument`](https://docs.umbraco.com/umbraco-cms/extend-your-project/tutorials/creating-a-backoffice-api/adding-a-custom-openapi-document) builds an OpenAPI document via .NET's native `Microsoft.AspNetCore.OpenApi`, filtered to only the endpoints carrying a matching `[MapToApi(Constants.ApiName)]` — which is exactly the attribute Step 1's base controller carries, with the same string. That's the whole tie-together: `Constants.ApiName` (`"umbracocommunityblockrestrictions"`) has to match in exactly two places — the controller's `[MapToApi(...)]` and the composer's `AddBackOfficeOpenApiDocument(...)` call — and a comment on the constant itself says as much, in case the two ever drift.

Worth being precise about the word "Swagger" here, since it's still what most people say out loud: as of Umbraco 18, document *generation* is .NET's own OpenAPI stack, not Swashbuckle's `SwaggerGen`. Swashbuckle's UI package is still in the mix for the browsable page, and the resolved route for that page moved too — `/umbraco/openapi`, not the pre-v18 `/umbraco/swagger`. This repo's own commit history has the migration, `c868cf1` ("chore: upgrade Umbraco to 18.0.2 (+ OpenAPI/Swagger migration)"), and its commit message is worth reading directly for the *why*:

> Umbraco 18 drops Swashbuckle for the backoffice Management API in favour of the built-in .NET OpenAPI stack. This removed the types our custom backoffice APIs depended on and broke the build: `Swashbuckle.AspNetCore.SwaggerGen` (namespace gone), `BackOfficeSecurityRequirementsOperationFilterBase` (operation-filter base)... Migrate all three custom API registrations to the v18 pattern.

The diff in that same commit is the clearest "before and after" you'll find — this package's composer used to carry a `Configure<SwaggerGenOptions>` block and a custom `BlockRestrictionsOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase`, both deleted in favour of the three-line `AddBackOfficeOpenApiDocument` call above.

### Step 4 — The concrete controller carries only actions

[`BlockRestrictionApiController.cs`](../../../src/UmbracoCommunity.BlockRestrictions/Controllers/BlockRestrictionApiController.cs) has eleven actions and not one routing or auth attribute — everything from Step 1 already applies. Two worth a closer look:

```csharp
[HttpGet("allowed-blocks/{nodeKey:guid}")]
[ResponseCache(Duration = 60, Location = ResponseCacheLocation.Client)]
public async Task<IActionResult> GetAllowedBlocks(
    Guid nodeKey, [FromQuery] Guid? contentTypeKey = null, [FromQuery] Guid? parentKey = null)
{
    var result = await _service.ResolveAllowedBlocksForNodeAsync(nodeKey);
    if (result == null && (contentTypeKey.HasValue || parentKey.HasValue))
    {
        result = await _service.ResolveForNewContentAsync(contentTypeKey, parentKey);
    }
    if (result == null) return NotFound();
    return Ok(result);
}
```

This is the endpoint the restricted property editors call on load, and its fallback path is a nice piece of design worth noticing on its own: a *new*, unsaved content node has no `nodeKey` yet, so the property editor passes `contentTypeKey`/`parentKey` instead and the controller resolves restrictions from those. The doc comment above this method in the real file states the security-adjacent default plainly: "Returns `HasRestrictions=false` if no rules are found at any level (fail-open)" — a deliberate, permissive default for *this* feature, worth contrasting with Step 2's restrictive-by-default backoffice auth. Different defaults for different risk profiles, both deliberate.

```csharp
[HttpPost("file-import/upload")]
public IActionResult UploadZip(IFormFile file)
{
    if (file == null || file.Length == 0)
    {
        return BadRequest("No file uploaded.");
    }
    using var stream = file.OpenReadStream();
    var result = _service.ImportZipToFiles(stream);
    return Ok(result);
}
```

`[ApiController]` (from Step 1's attributes) infers form-body binding for a plain `IFormFile` parameter automatically — no `[FromForm]` needed. This is also the one explicit `BadRequest` in the whole controller; every other action either succeeds, `Ok()`s an empty/found result, or `NotFound()`s.

### Step 5 — Two different kinds of "discovery," easy to conflate

Nothing registers this controller with ASP.NET Core routing beyond the attributes already shown. `UmbracoCommunity.BlockRestrictions` is a Razor Class Library referenced from the host via a plain `<ProjectReference>` — that's a compile-time reference, so ASP.NET Core's own `ApplicationPartManager` walks the dependency graph and finds the controller automatically. No `AddApplicationPart` call anywhere in this repo.

Don't confuse that with `AddComposers()` in `Program.cs` — that's Umbraco's *own* startup-hook discovery, scanning loaded assemblies for `IComposer` implementations (like `BlockRestrictionComposer`, which is where Step 3's OpenAPI registration and the package's DI registrations live) and running them at boot. Two different discovery mechanisms, from two different frameworks, that happen to both "just work" for the same RCL for unrelated reasons.

### Step 6 — The typed client, and the one call site that deliberately skips it

[`client.ts`](../../../src/UmbracoCommunity.BlockRestrictions/Client/src/api/client.ts) is the hand-written flavour the [backoffice primer](../../primers/backoffice.md#talking-to-the-c-apis) already sketches — a module-level `_authConfig` set once via `setAuthConfig()` (called from a Lit element consuming `UMB_AUTH_CONTEXT`), and every request going through:

```typescript
const API_BASE = "/umbraco/umbracocommunityblockrestrictions/api/v1";

async function fetchWithAuth(url: string, options: RequestInit = {}): Promise<Response> {
  const token = await resolveToken();
  const headers: Record<string, string> = {
    "Content-Type": "application/json",
    ...(options.headers as Record<string, string>),
  };
  if (token) headers["Authorization"] = `Bearer ${token}`;
  return fetch(`${_authConfig.baseUrl ?? ""}${url}`, {
    ...options, headers, credentials: _authConfig.credentials ?? "same-origin",
  });
}
```

`API_BASE` is hand-kept in sync with Step 1's `[BackOfficeRoute(...)]` string — there's no build-time check that they agree, which is exactly the drift risk the backoffice primer flags for hand-written clients over generated ones.

`uploadZip` deliberately doesn't go through `fetchWithAuth`, and the reason is a one-line comment worth internalising: it needs to *omit* `Content-Type` entirely so the browser can set the multipart form boundary itself. A generic wrapper that always sets `Content-Type: application/json` would silently break every file upload.

(Extensions' package takes the other approach entirely — a generated client from `@hey-api/openapi-ts`, wired to `UMB_AUTH_CONTEXT` at each call site instead of a shared module-level config. The [backoffice primer](../../primers/backoffice.md#talking-to-the-c-apis) covers that contrast; this tutorial won't repeat it.)

## Alternatives we considered

- **Consuming Umbraco's own Management API surface instead of a custom controller.** Viable for anything Umbraco already models — content, media, users. Not viable here: block restriction rules, 404 hit logs, and Discord announcement runs are domain data Umbraco's API has no concept of. A custom controller is the only option once your data isn't Umbraco's own.
- **A generated TypeScript client instead of the hand-written one.** Covered above and in the backoffice primer — a real trade-off (codegen sync vs. hand-maintained drift risk), not a wrong choice either way.
- **Splitting auth by risk** — looser policies for read-only lookups (`element-types`, `block-data-types`) than for the write endpoints. Not what shipped: every action in every one of this repo's four custom API packages uses the same `SectionAccessContent` policy, uniformly. One policy, one thing to reason about, at the cost of not being able to expose read-only data more broadly without opening the whole controller.

## Trade-offs and known limits

- **No automated tests.** There's no test project for `BlockRestrictionApiController` (or any of its three siblings) anywhere in this repo — routing, authorization, and action behaviour are all currently unverified by CI.
- **Package-scoped routes miss Umbraco's own exception-handling pipeline.** Umbraco's Management API has a global `ProblemDetails`-shaped exception handler, but it only covers routes under `.../management/api/` — this package's routes live under a different prefix (`.../umbracocommunityblockrestrictions/api/...`) entirely, so an unhandled exception here falls back to plain ASP.NET Core behaviour, not Umbraco's JSON error shape. Worth confirming directly if you're relying on a specific error response format.
- **Versioning convention isn't identical across every sibling.** Block Restrictions, NotFoundTracker, and Blog Announcements all put `[ApiVersion("1.0")]` on the shared base controller; Extensions' base controller has no `[ApiVersion]` at all — each of *its* three concrete controllers carries the attribute individually instead. Both arrangements resolve the same `{version:apiVersion}` route token correctly, so nothing is broken — but a new Extensions controller that forgets the attribute would be, in a way the other three packages' shared-base convention doesn't risk.

## Where to go next

- **[Backoffice extensions primer](../../primers/backoffice.md)** — the frontend half of this same package: manifests, `@umbraco-cms/backoffice`, `UMB_AUTH_CONTEXT`.
- **[Backend primer](../../primers/backend.md)** — composers in general, and where `AddBackOfficeOpenApiDocument` sits relative to the rest of a package's startup wiring.
- **[`src/UmbracoCommunity.BlockRestrictions/README.md`](../../../src/UmbracoCommunity.BlockRestrictions/README.md)** — the deepest worked example of this whole package end to end.

Hopefully that's the other half of "how do I add a backoffice API" that most tutorials stop short of — welcome aboard!

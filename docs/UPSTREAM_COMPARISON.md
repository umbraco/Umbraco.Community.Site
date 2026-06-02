# Upstream-to-Community Port: Comparison Report

Comparison of the upstream commercial site (UmbracoCom) against the community site (UmbracoCommunity) to identify beneficial changes to port across.

**Date:** 2026-02-24
**Already ported:** Image quality/webp fix, Intercom removal + unused component cleanup

---

## Summary Table

| # | Title | Priority | Category | Effort |
|---|-------|----------|----------|--------|
| 1 | ViteLinkTagHelper missing `EntryNameWithBase` for additional CSS | **High** | Bug Fix | Small |
| 2 | Add HSTS and error handling to Program.cs | **High** | Security / Resilience | Small |
| 3 | Add static error page (error.html) | **High** | Resilience / UX | Small |
| 4 | Add FormValidationMiddleware for bot protection | **Medium** | Security | Small |
| 5 | Add WebHostEnvironmentExtensions (Local env support) | **Medium** | Dev Experience | Small |
| 6 | Add `:host` to CSS custom properties | **Medium** | CSS / Frontend | Small |
| 7 | Add response caching with media exclusion | **Medium** | Performance | Small |
| 8 | Add frontend retry utility | **Medium** | Resilience | Small |
| 9 | Upgrade Umbraco CMS 17.1.0 to 17.2.0 | **Medium** | Dependencies | Medium |
| 10 | Add frontend logger utility | **Low** | Dev Experience | Small |
| 11 | Add colors.ts for accessible color contrast | **Low** | Accessibility | Small |
| 12 | Add fetch.ts wrapper service | **Low** | Frontend / Quality | Small |
| 13 | Add HasForms HtmlHelper extension | **Low** | Backend / Quality | Small |
| 14 | Modernize DisableCspMiddleware syntax | **Low** | Code Quality | Small |

## Recommended Porting Order

**Phase 1 -- Immediate (High priority, small effort):**
Items 1, 2, 3. These fix a real bug in the Vite CSS asset pipeline and close security/resilience gaps.

**Phase 2 -- Near-term (Medium priority):**
Items 4, 5, 6, 7, 8, 9. Security, developer experience, frontend compatibility, performance, resilience, and dependency updates.

**Phase 3 -- When convenient (Low priority):**
Items 10, 11, 12, 13, 14. Quality-of-life improvements.

---

## Detailed Items

### 1. ViteLinkTagHelper Bug: Missing `EntryNameWithBase` for Additional CSS Files

**Priority:** High | **Effort:** Small | **Category:** Bug Fix

When a Vite manifest entry contains multiple CSS files, the `ViteLinkTagHelper` iterates over `viteManifestEntry.Css[1..]` and renders additional `<link>` tags. The upstream wraps each CSS path with `EntryNameWithBase(css)` to prefix the correct static assets base path (`/assets/`). The community project uses the raw `css` value, which means secondary CSS files in multi-CSS bundles will reference incorrect paths in production.

**Upstream file:** `src/UmbracoDotCom.Web/Vite/TagHelpers/ViteLinkTagHelper.cs`
```csharp
linkTag.Attributes["href"] = EntryNameWithBase(css);
```

**Community file:** `src/UmbracoCommunity.Web/Vite/TagHelpers/ViteLinkTagHelper.cs`
```csharp
linkTag.Attributes["href"] = css;  // Missing EntryNameWithBase() wrapper
```

**Fix:** Change the community line to `linkTag.Attributes["href"] = EntryNameWithBase(css);`

---

### 2. Add HSTS and Error Handling to Program.cs

**Priority:** High | **Effort:** Small | **Category:** Security / Resilience

The upstream has proper environment-based error handling in `Program.cs`: `UseDeveloperExceptionPage()` in development, `UseExceptionHandler("/error.html")` plus `UseHsts()` in production. The community project has none of this. Without it, unhandled exceptions in production could expose stack traces to users, and browsers are never instructed to use HTTPS exclusively.

**Upstream file:** `src/UmbracoDotCom.Web.UI/Program.cs` (lines 30-38)
```csharp
if (app.Environment.IsDevelopmentEnvironment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error.html");
    app.UseHsts();
}
```

**Community file:** `src/UmbracoCommunity.Web.UI/Program.cs` -- no equivalent code exists between `builder.Build()` and `BootUmbracoAsync()`.

**Fix:** Add equivalent error handling and HSTS before the `BootUmbracoAsync()` call. Use `IsDevelopment()` (or `IsDevelopmentEnvironment()` from item 5 if ported).

---

### 3. Add Static Error Page (error.html)

**Priority:** High | **Effort:** Small | **Category:** Resilience / UX

The upstream has a self-contained static `error.html` page served when unhandled exceptions occur. It uses inline styles and fonts (so it renders even when all asset pipelines are broken), includes the Umbraco logo, a friendly error message, and a contact email link. The community project has no error page at all -- if `UseExceptionHandler` is added (item 2), it needs a target.

**Upstream file:** `src/UmbracoDotCom.Web.UI/wwwroot/error.html`
**Community file:** Does not exist.

**Fix:** Create a `wwwroot/error.html` in the community project adapted for the community site branding and contact information.

---

### 4. Add FormValidationMiddleware for Bot Protection

**Priority:** Medium | **Effort:** Small | **Category:** Security

The upstream includes `FormValidationMiddleware` that intercepts POST requests to `/umbraco/forms` endpoints and validates the `formId` field. It rejects requests with malformed GUIDs (returns 400) and requests with empty GUIDs (a common bot pattern). It also logs the remote IP address for monitoring. The community project does not have this protection.

**Upstream file:** `src/UmbracoDotCom.Web/Middleware/FormValidationMiddleware.cs`

Key logic:
```csharp
if (!Guid.TryParse(formId, out Guid parsedFormId))
{
    _logger.LogWarning("Bot attempt: Invalid form ID format '{FormId}' from IP {IpAddress}",
        formId, context.Connection.RemoteIpAddress);
    context.Response.StatusCode = StatusCodes.Status400BadRequest;
    return;
}
if (parsedFormId == EmptyGuid) { /* reject */ }
```

**Community file:** Does not exist.

**Fix:** Port the middleware class (adjusting namespace) and register it in `Program.cs` with `app.UseMiddleware<FormValidationMiddleware>()`.

---

### 5. Add WebHostEnvironmentExtensions for "Local" Environment Support

**Priority:** Medium | **Effort:** Small | **Category:** Development Experience

The upstream has a `WebHostEnvironmentExtensions` class that introduces the concept of a "Local" environment in addition to "Development". The helper `IsDevelopmentEnvironment()` returns true for both. This allows distinguishing between a developer machine ("Local") and a cloud development/staging slot ("Development").

**Upstream file:** `src/UmbracoDotCom.Web/Extensions/WebhostEnvironmentExtensions.cs`
```csharp
public static bool IsDevelopmentEnvironment(this IWebHostEnvironment env)
    => env.IsDevelopment() || env.IsLocalEnvironment();

public static bool IsLocalEnvironment(this IWebHostEnvironment env)
    => env.IsEnvironment("Local");
```

**Community file:** Does not exist.

**Fix:** Port the extension class and optionally update `ViteTagHelperBase.IsDevelopmentEnvironment()`, security headers, and robots controller to use it.

---

### 6. Add `:host` Selector to CSS Custom Properties for Web Component Support

**Priority:** Medium | **Effort:** Small | **Category:** CSS / Frontend

The upstream's `root.css` declares CSS custom properties on both `:root` and `:host`. The community project only uses `:root`. Since both projects use Lit web components with Shadow DOM, the `:host` selector ensures CSS custom properties are naturally available inside shadow roots without needing workarounds.

**Upstream file:** `src/UmbracoDotCom.StaticAssets/src/css/root.css`
```css
:root,
:host {
```

**Community file:** `src/UmbracoCommunity.StaticAssets/src/css/root.css`
```css
:root {
```

**Fix:** Change `:root {` to `:root, :host {` in the community `root.css`.

---

### 7. Add Response Caching with Media Exclusion

**Priority:** Medium | **Effort:** Small | **Category:** Performance

The upstream uses `UseResponseCaching()` middleware with a conditional exclusion for `/media` requests (to avoid caching duplication with ImageSharp's built-in caching). The community project does not use response caching middleware. This complements existing output caching -- response caching sets `Cache-Control` headers for browser/CDN caching, while output caching stores responses server-side.

**Upstream file:** `src/UmbracoDotCom.Web.UI/Program.cs` (lines 40-44)
```csharp
app.UseWhen(
    n => !n.Request.Path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.UseResponseCaching()
);
```

**Community file:** `src/UmbracoCommunity.Web.UI/Program.cs` -- not present.

**Fix:** Add response caching middleware with the media exclusion before `BootUmbracoAsync()`.

---

### 8. Add Frontend Retry Utility

**Priority:** Medium | **Effort:** Small | **Category:** Resilience

The upstream has a `retry.ts` utility providing `withRetry()` for async operations with exponential backoff, jitter to prevent thundering herd, and `isRetryableError()` that distinguishes retryable errors (5xx, 408, 429, network errors) from non-retryable ones (4xx). This would benefit the community project's Sessionize service and blog service calls.

**Upstream file:** `src/UmbracoDotCom.StaticAssets/src/util/retry.ts`

Key API:
```typescript
export async function withRetry<T>(
  operation: () => Promise<T>,
  options: RetryOptions = {}
): Promise<T>

export function isRetryableError(error: Error): boolean
```

**Community file:** Does not exist.

**Fix:** Port `retry.ts` into the community project's `src/util/` directory.

---

### 9. Upgrade Umbraco CMS from 17.1.0 to 17.2.0

**Priority:** Medium | **Effort:** Medium | **Category:** Dependencies / Security / Bug Fixes

The upstream uses Umbraco CMS 17.2.0 while the community project uses 17.1.0. Minor version upgrades typically include bug fixes, security patches, and performance improvements.

**Upstream file:** `Directory.Packages.props` -- Umbraco.Cms `Version="17.2.0"`
**Community file:** `Directory.Packages.props` -- Umbraco.Cms `Version="17.1.0"`

**Fix:** Update the Umbraco packages in `Directory.Packages.props` from 17.1.0 to 17.2.0. Test thoroughly before deploying.

---

### 10. Add Frontend Logger Utility

**Priority:** Low | **Effort:** Small | **Category:** Development Experience / Debugging

The upstream has a structured `logger.ts` utility with `LoggerFactory`, namespaced loggers, configurable log levels, and environment-aware configuration. This replaces ad-hoc `console.log`/`console.error` calls with a consistent, controllable logging system.

**Upstream file:** `src/UmbracoDotCom.StaticAssets/src/util/logger.ts`

Key API:
```typescript
export class LoggerFactory {
  static createLogger(config: LoggerConfig): Logger
  static getLogger(namespace: string): Logger | undefined
}
```

**Community file:** Does not exist.

**Fix:** Port `logger.ts` into the community project's `src/util/` directory.

---

### 11. Add colors.ts for Accessible Color Contrast Calculation

**Priority:** Low | **Effort:** Small | **Category:** Accessibility

The upstream has a `colors.ts` utility with `hexToRgb()`, `isColorDark()`, and `getAccessibleTextColor()` using WCAG brightness calculation. The community project's C# `ColourHelper.ColourIsDark()` uses a hardcoded array of "dark" colour values, which is fragile and will break if new colours are added.

**Upstream file:** `src/UmbracoDotCom.StaticAssets/src/util/colors.ts`
```typescript
export function getAccessibleTextColor(color: string) {
  const colorAsRgb = hexToRgb(color);
  const brightness = Math.round(
    (colorAsRgb.r * 299 + colorAsRgb.g * 587 + colorAsRgb.b * 114) / 1000
  );
  return brightness > 125 ? "var(--black, #000000)" : "var(--white, #ffffff)";
}
```

**Community file (C# equivalent):** `src/UmbracoCommunity.Web/Helpers/ColourHelper.cs`
```csharp
private static bool ColourIsDark(string colour)
{
    var darkBgs = new[] { "#3544B1", "#1b264f" }; // hardcoded
    return darkBgs.Any(x => string.Equals(x, colour, ...));
}
```

**Fix:** Port `colors.ts` for frontend use. Additionally, update C# `ColourIsDark()` to use actual brightness calculation.

---

### 12. Add fetch.ts Wrapper Service

**Priority:** Low | **Effort:** Small | **Category:** Frontend / Code Quality

The upstream has a `fetch.ts` wrapper providing a consistent `{ data, error }` return pattern for HTTP calls, with automatic JSON parsing and centralised error handling.

**Upstream file:** `src/UmbracoDotCom.StaticAssets/src/services/fetch.ts`

**Note:** The upstream code has a bug on line 18 where `error = error` should be `error = err`. Fix this when porting.

**Community file:** Does not exist.

**Fix:** Port `fetch.ts` into the community project's `src/services/` directory, fixing the bug.

---

### 13. Add HasForms HtmlHelper Extension

**Priority:** Low | **Effort:** Small | **Category:** Backend / Code Quality

The upstream `HtmlHelperExtensions` includes a `HasForms()` method that checks whether the current HTTP context contains Umbraco Forms. This allows conditionally loading form-related scripts and stylesheets only on pages that actually use forms.

**Upstream file:** `src/UmbracoDotCom.Web/Extensions/HtmlHelperExtensions.cs`
```csharp
public static bool HasForms(this IHtmlHelper helper, HttpContext httpContext)
    => httpContext.Items["UmbracoForms"] is IEnumerable<Guid> formIds && formIds.Any();
```

**Community file:** `src/UmbracoCommunity.Web/Extensions/HtmlHelperExtensions.cs` -- does not include this method.

**Fix:** Add the `HasForms` method to the community `HtmlHelperExtensions`. Then use it in the layout to conditionally render form dependencies.

---

### 14. Modernize DisableCspMiddleware Syntax

**Priority:** Low | **Effort:** Small | **Category:** Code Quality

The community project's `DisableCspMiddleware.cs` uses the older block-scoped namespace syntax, has a redundant fully-qualified `Microsoft.AspNetCore.Http.HttpContext` parameter type, and includes unnecessary explicit `using` statements. The upstream version is cleaner with file-scoped namespace and implicit usings.

**Upstream file:** `src/UmbracoDotCom.Web/Middleware/DisableCspMiddleware.cs`
**Community file:** `src/UmbracoCommunity.Web/Middleware/DisableCspMiddleware.cs`

**Fix:** Modernize to file-scoped namespace and clean up using directives and type qualifications.

---
tags: [csp, security, razor, nonce, tag-helper]
---

# How to keep inline scripts and styles working under a strict Content Security Policy

A strict [Content Security Policy](https://developer.mozilla.org/en-US/docs/Web/HTTP/Headers/Content-Security-Policy) that bans inline scripts catches most XSS vectors at the browser level — the browser simply refuses to run a `<script>` tag that isn't on an approved list. The trouble is that a real site *has* inline scripts and styles: build-tool bootstrap snippets, per-instance background colours an editor picked in the backoffice, structured-data JSON-LD. Ban all of them and half the site breaks; allow `'unsafe-inline'` and you've defeated the point of having a CSP at all. The standard middle ground is a **nonce** — a random token generated once per request, stamped onto both the CSP header and every inline tag you actually trust, so the browser runs only the ones whose token matches. This is a *foundation* piece: nothing else in this suite builds on it, but it underpins every inline `<script>` and `<style>` in the codebase.

## Why you might want this

If you've only ever met CSP as a Lighthouse warning to silence, the nonce approach is worth knowing about because it's the only one of the three common fixes that doesn't cost you something significant. `'unsafe-inline'` on `script-src` makes the warning go away and CSP itself pointless — it now permits *any* inline script, which is exactly what XSS payloads are. Moving every inline script to an external file is the textbook-correct answer, but it doesn't work for content that's generated per-request from editor input (a block's background colour, computed per content-block instance) — there's no static file to point at. A nonce lets you keep the inline tag *and* keep the policy strict, at the cost of remembering to stamp the nonce on every tag that needs it.

## What we're building

Four pieces, most of them thin wrappers around the [`Joonasw.AspNetCore.SecurityHeaders`](https://github.com/juunas11/aspnetcore-security-headers) package rather than anything hand-rolled:

1. **A per-request nonce service** (`ICspNonceService`, from the package) generating one random value, shared by everything that needs it during that request.
2. **A `NonceTagHelper`** that turns an `asp-add-nonce="true"` attribute on `<script>`, `<style>`, or `<link>` into the matching `nonce="..."` attribute.
3. **CSP directive configuration** — one call registering the header, with `script-src` getting the nonce and a small set of domain allow-lists per directive for the third parties this site actually embeds (YouTube, Sessionize, GitHub, a couple of image CDNs).
4. **A content-driven escape hatch**, `DisableCspMiddleware`, for the day some block needs CSP switched off entirely rather than nonced.

## Why the obvious fix doesn't work

**Reaching for `'unsafe-inline'` the moment CSP blocks something.** This is the fix that "works" fastest and is wrong every time: it silences the browser console error by telling the browser to stop enforcing the exact thing CSP exists to enforce. If you find yourself about to add `'unsafe-inline'` to `script-src` to make a warning disappear, that's the signal to reach for a nonce instead.

**Generating a fresh nonce every time you need one.** The name "nonce" (number used once) makes it tempting to call `RandomNumberGenerator` fresh inside the tag helper or wherever you need a token — but the CSP header for a response carries exactly *one* nonce value, and every inline tag on that page has to match it. Generate a new one per call and only the last tag stamped wins; every earlier one silently fails to match and gets blocked. The nonce has to be generated once per **request** and shared by every consumer — header and tags alike — which is why this is a scoped service, not a static helper method.

**Forgetting the attribute when you copy-paste a tag.** This isn't theoretical — it happened in this exact codebase. An early version of the layout emitted the Vite client bootstrap script and the per-page-type bundle script twice (once in `<head>`, once again just before `</body>`), and one of the four copies was missing `asp-add-nonce` entirely. The fix (commit `cc0c3a5`, "Add nonces to both scripts, and remove the duplicated scripts later on") removed the duplicate emission and added the missing attribute to the one that remained — but the failure mode is worth knowing: a script tag with no nonce attribute at all doesn't error loudly in your editor or at build time, it just silently fails to execute once script-src stops allowing unnonced inline content, and you find out from a broken page in the browser console.

## Walkthrough

### Step 1 — Register the nonce service and HSTS

[`Extensions/UmbracoBuilderExtensions.cs`](../../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs):

```csharp
public static IUmbracoBuilder AddSecurityPolicies(this IUmbracoBuilder builder)
{
    builder.Services.AddCsp();
    builder.Services.AddHsts(options =>
    {
        options.Preload = true;
        options.IncludeSubDomains = true;
        options.MaxAge = TimeSpan.FromSeconds(31536000); // 1 year, minimum recommended https://www.upguard.com/blog/hsts
    });

    return builder;
}
```

`builder.Services.AddCsp()` is the package's own registration call, and it does exactly one thing worth knowing: `services.AddScoped<ICspNonceService>(sp => new CspNonceService(nonceByteAmount))`. **Scoped** is the detail that answers the "generate it once" problem above — one service instance per HTTP request, and that instance generates its 32 random bytes exactly once, in its constructor, caching them as a base64 string that every subsequent `GetNonce()` call just returns. `AddSecurityPolicies()` is chained into `CreateUmbracoBuilder()` in [`Program.cs`](../../../src/UmbracoCommunity.Web.UI/Program.cs) alongside the other cross-cutting registrations (see the [backend primer](../../primers/backend.md#bootstrapping) for that chain).

### Step 2 — Configure the CSP directives

The actual policy lives in [`Extensions/WebApplicationExtensions.cs`](../../../src/UmbracoCommunity.Web/Extensions/WebApplicationExtensions.cs):

```csharp
public static void UseSecurityHeaders(this WebApplication app)
{
    app.Use(async (context, next) =>
    {
        // X-Xss-Protection, Referrer-Policy, X-Frame-Options, X-Content-Type-Options,
        // Permissions-Policy headers set here too — omitted for brevity.
        await next();
    });

    app.UseCsp(csp =>
    {
        SetProductionCspRules(csp);

        if (app.Environment.IsDevelopment())
        {
            SetDevelopmentCspRules(csp);
        }

        csp.OnSendingHeader = context =>
        {
            context.ShouldNotSend = context.HttpContext.Request.Path.StartsWithSegments("/umbraco");
            return Task.CompletedTask;
        };
    });
}

private static void SetProductionCspRules(CspBuilder csp)
{
    csp.ByDefaultAllow.FromSelf().FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowDomains);
    csp.AllowScripts.FromSelf().FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowScripts).AddNonce();
    csp.AllowStyles.FromSelf().FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowStyles).AllowUnsafeInline();
    csp.AllowImages.FromSelf().FromAll((builder, domain) => builder.From(domain), Constants.Security.DefaultAllowImages);
    // ...frames, fonts, connections, form-actions, workers, audio/video follow the same shape
}
```

`app.UseCsp(...)` is the package's middleware registration — it builds a `CspOptions` from the builder action and wraps the whole pipeline in `CspMiddleware`. `.AddNonce()` on `AllowScripts` (and only there) is the one line that turns on nonce checking for `script-src`; every other directive is either a plain domain allow-list or, for styles, `.AllowUnsafeInline()` — worth flagging now and coming back to in Trade-offs: **`style-src` allows unsafe-inline outright, so the nonce on `<style asp-add-nonce="true">` tags isn't actually load-bearing today.**

The domain lists come from one place, [`Constants.Security`](../../../src/UmbracoCommunity.Web/Constants.Security.cs) — e.g. `DefaultAllowImages` includes `avatars.githubusercontent.com`, `sessionize.com` and its CDN subdomains, `api.dicebear.com`; `DefaultAllowFrames` has `*.youtube.com` and `*.walls.io`; `DefaultAllowFormActions` has `github.com` (the OAuth sign-in POST target — see the [GitHub OAuth tutorial](github-oauth-member-authentication.md)). One per-directive add call only takes a single domain at a time in the underlying package's API, which is why [`CspBuilderExtensions.cs`](../../../src/UmbracoCommunity.Web/Extensions/CspBuilderExtensions.cs) exists — `ToAll`/`FromAll` are small loops that let `SetProductionCspRules` pass a whole array in one line instead of one `.From(...)` call per domain.

`csp.OnSendingHeader` is the last piece: it skips sending the header at all for anything under `/umbraco` — the backoffice runs its own JS/CSS bundle with its own inline-script needs, and this site's policy isn't meant to govern it.

### Step 3 — Mind the pipeline order

[`Program.cs`](../../../src/UmbracoCommunity.Web.UI/Program.cs):

```csharp
app.UseHttpsRedirection();
app.UseSecurityHeaders();      // CSP registered here — wraps everything after it
app.UseMiddleware<FormValidationMiddleware>();
app.UseOutputCache();
app.UseUmbraco()
    .WithMiddleware(u => { u.UseBackOffice(); u.UseWebsite(); })
    .WithEndpoints(u => { /* ... */ });
```

`UseSecurityHeaders()` — and therefore the CSP middleware — runs *before* `UseUmbraco()`, so it wraps the backoffice and website middleware alike (which is exactly why the `/umbraco` exclusion has to happen inside the CSP middleware's own `OnSendingHeader` hook rather than by pipeline ordering — there's no separate "backoffice stage" to order around, it's all nested inside one `UseUmbraco()` call).

### Step 4 — The tag helper

[`Vite/TagHelpers/NonceTagHelper.cs`](../../../src/UmbracoCommunity.Web/Vite/TagHelpers/NonceTagHelper.cs) — it lives next to the Vite integration rather than in the general `TagHelpers/` folder, because it was built alongside the Vite manifest tag helpers rather than as a standalone security feature:

```csharp
[HtmlTargetElement("script", Attributes = "asp-add-nonce")]
[HtmlTargetElement("style", Attributes = "asp-add-nonce")]
[HtmlTargetElement("link", Attributes = "asp-add-nonce")]
public class NonceTagHelper : TagHelper
{
    private readonly ICspNonceService _nonceService;

    [HtmlAttributeName("asp-add-nonce")]
    public bool AddNonce { get; set; }

    public NonceTagHelper(ICspNonceService nonceService) => _nonceService = nonceService;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (AddNonce)
        {
            output.Attributes.Add("nonce", _nonceService.GetNonce());
        }
    }
}
```

Three target elements, one gating attribute, one job: add a `nonce="..."` attribute carrying whatever `ICspNonceService.GetNonce()` returns for *this* request — the same scoped instance from Step 1, so the value here is guaranteed to match the value the CSP middleware stamps onto the response header. `Process` never touches tag content, only `output.Attributes` — same minimal-mutation shape as the [inline SVG TagHelper](inline-svg-tag-helper.md), just adding an attribute instead of replacing content.

### Step 5 — Use it from Razor

Two shapes cover almost every use in this repo. A script or link that needs the nonce:

```cshtml
<script vite-src="index" vite-client="true" asp-add-nonce="true"></script>
```

And the far more common one — a scoped inline `<style>` block carrying an editor-configured value, keyed by a per-instance id so it only targets one block:

```cshtml
<style asp-add-nonce="true">
    #@Model.Content.IdHash { --block-background-color: @Model.Settings?.BackgroundColour?.Color; }
</style>
```

That second shape — `<style asp-add-nonce="true">#@Model.Content.IdHash { ... }</style>` — repeats across the majority of content blocks in [`Views/Partials/Blocks/`](../../../src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/): `TextBlock`, `ImageBlock`, `CardsBlock`, `SliderBlock`, and a dozen others all use it for the same thing, an editor-picked `BackgroundColour` rendered as a scoped CSS custom property (the `IdHash` convention is covered in the [content-modelling primer](../../primers/content-modelling.md#extending-generated-models-with-hand-written-partials)). Structured-data JSON-LD gets the same treatment in [`MetaTags.cshtml`](../../../src/UmbracoCommunity.Web.UI/Views/Shared/Components/MetaTags/MetaTags.cshtml) — see the [SEO primer](../../primers/seo-and-structured-data.md#schema-builders) for what generates that markup.

### Step 6 — The escape hatch, and where it currently stands

[`Middleware/DisableCspMiddleware.cs`](../../../src/UmbracoCommunity.Web/Middleware/DisableCspMiddleware.cs) is wired into Umbraco's own pipeline-filter system (`AddPipelineFilters()`, `postPipeline`) rather than a plain `app.UseMiddleware<>()` call, so it runs after the published request has resolved:

```csharp
public async Task InvokeAsync(HttpContext context)
{
    if (_umbracoContextAccessor.TryGetUmbracoContext(out var umbracoContext)
        && umbracoContext?.PublishedRequest?.PublishedContent is not null)
    {
        if (ContainsCspHeader(context.Response) && ContainsInsecureBlocks(umbracoContext.PublishedRequest.PublishedContent))
        {
            context.Response.Headers.Remove(CspHeaderName);
        }
    }

    await _next(context);
}
```

This is worth being precise about, because it's easy to assume it's a live feature: it's **content-driven**, not a route attribute or per-endpoint opt-out — it inspects the *currently rendering page's* Block List properties for any block whose content-type alias is in a configured list, and if it finds one, strips the already-set `Content-Security-Policy` header for that response entirely (not narrow it — the whole header goes). As shipped, the list of blocks that trigger this (`_disabledCspBlocks`) is empty, so the middleware can never actually fire today. The seam exists — for the day some third-party embed injects inline `<script>` tags at runtime that there's no way to nonce — but nothing currently uses it.

## Alternatives we considered

- **Hash-based CSP** (`'sha256-...'` sources instead of a nonce) lets the browser match inline content by its hash rather than a per-request token, which sidesteps the "did every tag get the attribute" failure mode entirely. It doesn't fit this codebase's biggest inline-content case, though — a block's background colour is generated per content-block instance from editor input, so the hash would need recomputing on every publish, for every instance, which is more moving parts than a nonce that Just Works per request.
- **Extracting inline styles to external stylesheets** (e.g. one CSS class per possible colour, toggled by a data attribute) is the cleanest CSP story of all — no inline content, no nonce needed — but it doesn't extend to an arbitrary editor-picked colour without either a combinatorial explosion of classes or a runtime endpoint serving generated CSS, which is more infrastructure than the problem warrants.
- **CSP report-only mode first**, then switching to enforcing once violation reports come back clean, is the safer rollout path recommended by most CSP guides. This codebase never went through that phase — CSP shipped enforcing from day one, alongside the initial Vite build-pipeline setup, rather than as a retrofit onto an existing site. That's a reasonable choice for a CSP introduced at project inception; it would be a riskier one to skip on an existing site with unknown inline-script surface.

## Trade-offs and known limits

- **No CSP violation reporting.** The package supports `ReportOnly` and `ReportUri`, and neither is configured here. If a nonce goes missing on a new tag in production, there's no telemetry — you find out from a broken page in someone's browser console, not a report landing in a dashboard.
- **Style nonces are currently decorative.** `style-src` carries `.AllowUnsafeInline()`, so every `<style asp-add-nonce="true">` block in the codebase would render identically without the attribute — the nonce isn't doing enforcement work today. It's applied consistently anyway, which is either good future-proofing (the day `AllowUnsafeInline()` comes off styles, every tag is already ready) or dead weight, depending on how charitable you're feeling. Don't take its presence as proof that inline styles are nonce-gated; check the directive config to be sure.
- **The escape hatch needs a rebuild to use.** `DisableCspMiddleware`'s trigger list is a hardcoded array, not a backoffice setting — turning it on for a real block means a code change and deploy, not a content-editor toggle.
- **Domain allow-lists drift with third parties, silently.** Every Sessionize CDN move, new avatar provider, or added OAuth target has needed its own `Constants.Security` commit after the fact — there's no test or CI check that catches a newly-added external asset before it 404s past CSP in production.

## Where to go next

- The [inline SVG TagHelper tutorial](inline-svg-tag-helper.md) is the other TagHelper foundation in this suite, for the "output-attribute-mutation vs output-content-mutation" contrast.
- The [backend primer](../../primers/backend.md) covers where `AddSecurityPolicies()` sits in the wider composer/`Program.cs` bootstrapping chain.
- The [integrations primer](../../primers/integrations.md) is the accurate list of which third parties this site actually talks to — useful context for `Constants.Security`'s domain allow-lists, since not everything CLAUDE.md mentions in passing (Matomo, Intercom, a Maps embed) turned out to be real.

Hopefully that's the CSP-in-.NET post that didn't exist when we needed it — welcome aboard!

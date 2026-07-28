---
tags: [seo, schema-org, multi-tenant, social-settings]
---

# Tenant-aware fallback for schema and SEO metadata

> **Prerequisites:** This refinement builds on [Resolving content in a multi-tenant Umbraco site](../foundations/multi-tenant-content-resolution.md). The fallback chain below leans on the `GetSocialSettings()` extension method established there. If you haven't read the foundation, the resolution side of this tutorial will look like magic.

Structured data — the small blocks of [Schema.org](https://schema.org/) JSON-LD that crawlers like Google and Bing look for in your page head — needs a publisher: an `Organization` with a name, a URL, and a logo, that they can attribute the content to. On a multi-tenant Umbraco site, that publisher is naturally *per tenant* — Site A's publisher is the Umbraco Community brand, Site B's is the events microsite, and so on. The challenge is that tenant brand metadata is editor-configurable, which means (let us be honest with each other here) that it's also editor-forgettable. This tutorial walks through the small pattern that produces valid `Organization` schema whether the tenant's brand fields are filled in, partially filled in, or entirely absent.

> A quick gloss for anyone reading cold: **Schema.org** is a shared vocabulary for describing structured data on the web, and **JSON-LD** is the format (a flavour of JSON) that search engines prefer it embedded in. The C# library we use to build it is **[Schema.NET](https://github.com/RehanSaeed/Schema.NET)** — typed wrappers around the same vocabulary, so you get compile-time help instead of stringly-typed JSON.

## The problem

Google, Bing, and every other crawler expect publisher metadata in your `Article` and `WebPage` schema:

```json
{
  "@type": "Article",
  "headline": "...",
  "publisher": {
    "@type": "Organization",
    "name": "Umbraco Community",
    "url": "https://community.umbraco.com",
    "logo": "https://community.umbraco.com/media/.../logo.svg"
  }
}
```

In this codebase the publisher data lives on a `SocialSettings` document type — a child of the tenant's `Settings` node — with three editor-facing fields: `OrganisationName`, `OrganisationUrl`, `OrganisationLogo`. When an editor fills them in, the schema renders with their brand. When they don't, the schema still needs to render — both because emitting half-filled schema is invalid and rejected by validators, and because every page on the site has *some* `Article` or `WebPage` schema in the head, so the failure mode would be every page on a freshly-set-up tenant breaking its SEO until someone remembered to fill in three text boxes.

The brief reduces to:

1. Find this tenant's `SocialSettings` (the foundation pattern).
2. If it's there and populated, build the schema from it.
3. If it's missing or partial, fall back to something sensible — and "sensible" needs a clear definition.

## Why the obvious fix doesn't work

A few approaches that miss the mark in subtle ways:

**Require `SocialSettings` to be configured; throw if not.** Strictly correct in a "garbage in, no output" sense and trivially easy to write. The cost is every page on a newly-deployed tenant 500s until an editor remembers to fill in the brand fields. Editors forget. The fail-soft option is almost always the right call for content the site itself depends on.

**Emit an empty `Organization` (no name, no URL, no logo).** Validators reject it. Crawlers ignore it. Lighthouse drops your SEO score. No improvement over not emitting the schema at all.

**Read fallback values from `IGlobalSettings` (Umbraco's `Umbraco:CMS:Global` options).** `IGlobalSettings` is Umbraco's strongly-typed wrapper over the `Umbraco:CMS:Global` section of `appsettings.json` — `SiteName`, default time zone, request handling defaults, and so on. Tempting to lean on, because `IGlobalSettings.SiteName` exists and feels like the right default. But these are the *instance* defaults — set once at deployment, shared across all tenants. On a multi-tenant site they're either generic ("Umbraco Community Sites") or set to one tenant's brand (which then leaks into all the others). Neither is what we want.

**Per-tenant `appsettings.json` config keys.** "If `SocialSettings` is missing for tenant A, use `MultiTenant:TenantA:OrgName` from config." Now you've split tenant config across the backoffice *and* a JSON file. The whole point of having editor-configurable brand fields is so editors can update them. A deploy-time JSON fallback defeats that.

**The version that does work:** treat `SocialSettings` as nullable all the way through the schema builders, and supply a hardcoded sensible default at the very bottom of the chain. The defaults are explicit constants in the schema-building code — they're not pretending to be tenant-specific, they're "if a tenant has no brand configured yet, fall back to *this generic Umbraco-flavoured baseline* so the page still validates".

## Our approach

There is no need for us to reinvent the wheel here — the foundation pattern already does the tenant resolution part for us, so all we really need on top is a way of saying "and if the tenant hasn't configured itself yet, here's what we'll fall back to instead". Three pieces, each doing one thing:

1. **`SeoDataService`** is the resolver. It's the layer that knows about tenants, calls `currentPage.GetSocialSettings()`, and hands the result (possibly null) down to the schema builders.
2. **`OrganizationSchemaBuilder`** is the only builder that knows about the fallback. It accepts a nullable `SocialSettings` and produces a valid `Organization` either from the configured fields or from a small set of constants at the top of the class.
3. **Other schema builders** (`ArticleSchemaBuilder`, the `WebPage` schema fallback in `SeoDataService`) take a `SocialSettings?` and pass it to `OrganizationSchemaBuilder.Build(...)`. They don't know whether the result came from editor config or constants — and they don't need to.

The clean separation is the win. The resolver only does tenant resolution. The builders only do schema. The fallback is in one place, easy to find and easy to change. Adding a new schema type that needs a publisher means writing `_organizationSchemaBuilder.Build(socialSettings)` and forgetting the rest.

## Walkthrough

Three files. The whole chain is small enough to read end to end.

### Step 1 — Resolve `SocialSettings` from the current page

The entry point is [`SeoDataService.BuildAsync`](../../../src/UmbracoCommunity.Web/Services/SeoDataService.cs), which a view component calls on every page render to build the meta-tags view model:

```csharp
public async Task<MetaTagsViewModel> BuildAsync(IPublishedContent currentPage)
{
    var viewModel = new MetaTagsViewModel
    {
        Name = currentPage.Name
    };

    var siteSettings = currentPage.GetSiteSettings();
    viewModel.SiteName = siteSettings?.SiteName;

    if (currentPage is ICompositionSeo contentModel)
    {
        // …per-page SEO fields…

        var socialSettings = currentPage.GetSocialSettings(siteSettings);
        // …build schema, OG tags, etc using socialSettings (which may be null)…

        AddBaseSchema(viewModel, contentModel, socialSettings, currentPage);
    }

    return viewModel;
}
```

Two foundation calls back-to-back: `GetSiteSettings()` to get the tenant's `Settings` node, then `GetSocialSettings(siteSettings)` to chain off that same Settings node and find the `SocialSettings` child. The second call accepts the already-resolved `Settings` node as a parameter so it doesn't have to walk to the tenant root a second time — the small optimisation [the foundation tutorial covers](../foundations/multi-tenant-content-resolution.md#step-3--chaining-off-settings).

Either or both can return null. The view model still gets built; consumers downstream just see a `null` instead of a populated object.

### Step 2 — Hand the nullable `SocialSettings` down to the schema layer

`AddBaseSchema` is where the schema builders get called. Trimmed to the publisher-relevant bits:

```csharp
private void AddBaseSchema(
    MetaTagsViewModel viewModel,
    ICompositionSeo contentModel,
    SocialSettings? socialSettings,
    IPublishedContent currentPage)
{
    var articleSchema = _articleSchemaBuilder.Build(currentPage, socialSettings);
    if (articleSchema is not null)
    {
        viewModel.AddSchemaMarkup(articleSchema.ToHtmlEscapedString());
    }
    else
    {
        WebPage? webPageSchema = GetWebPageSchema(viewModel, contentModel, socialSettings);
        if (webPageSchema is not null)
        {
            viewModel.AddSchemaMarkup(webPageSchema.ToHtmlEscapedString());
        }
    }
}
```

`socialSettings` flows through unchanged. The article path calls `_articleSchemaBuilder.Build(currentPage, socialSettings)`. The non-article path falls through to a local `GetWebPageSchema` that does the same thing for `WebPage` schema. Both eventually need a publisher, and both delegate that to `OrganizationSchemaBuilder`.

### Step 3 — Build the `Organization` with a fallback

The whole [`OrganizationSchemaBuilder`](../../../src/UmbracoCommunity.Web/ViewModelBuilders/Schema/OrganizationSchemaBuilder.cs) is a single `Build` method on top of three constants:

```csharp
internal class OrganizationSchemaBuilder
{
    private const string DefaultOrganizationName = "Umbraco";
    private const string DefaultOrganizationUrl = "https://umbraco.com";
    private const string DefaultOrganizationLogo
        = "https://umbraco.com/media/ntljbdhh/umbraco_logo_blue.svg";

    public Organization Build(SocialSettings? socialSettings)
    {
        var organizationName = socialSettings?.OrganisationName;
        var organizationUrl = socialSettings?.OrganisationUrl;
        var organizationLogo = socialSettings?.OrganisationLogo;

        var hasCustomSettings = !string.IsNullOrEmpty(organizationName);

        var organization = new Organization
        {
            Name = hasCustomSettings ? organizationName! : DefaultOrganizationName
        };

        var urlToUse = hasCustomSettings && !string.IsNullOrEmpty(organizationUrl)
            ? organizationUrl
            : DefaultOrganizationUrl;

        Uri? baseUri = null;
        if (Uri.TryCreate(urlToUse, UriKind.Absolute, out var orgUri))
        {
            organization.Url = orgUri;
            baseUri = orgUri;
        }

        if (hasCustomSettings && organizationLogo is not null)
        {
            var logoUrl = organizationLogo.GetCropUrl("logo");
            if (!string.IsNullOrEmpty(logoUrl))
            {
                if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var absoluteLogoUri))
                {
                    organization.Logo = absoluteLogoUri;
                }
                else if (baseUri is not null
                    && Uri.TryCreate(baseUri, logoUrl, out var relativeLogoUri))
                {
                    organization.Logo = relativeLogoUri;
                }
            }
        }
        else if (Uri.TryCreate(DefaultOrganizationLogo, UriKind.Absolute, out var defaultLogoUri))
        {
            organization.Logo = defaultLogoUri;
        }

        return organization;
    }
}
```

A few decisions in there worth flagging:

**The fallback is constants, not config.** Hardcoded into the class file. Changing the fallback "Umbraco" name to something else requires a code change and a deploy — but that's *correct* for this codebase, because the fallback is the safety net for unconfigured tenants, not a thing editors should be able to swap out. If your project needs configurable fallbacks (e.g. white-label deployments where the safety net itself varies), lift the three constants to an `IOptions<…>`-bound config section. ([`IOptions<T>`](https://learn.microsoft.com/aspnet/core/fundamentals/configuration/options) is ASP.NET Core's standard pattern for "bind a section of `appsettings.json` to a typed C# class and inject it"; you'll see it used elsewhere in this codebase for the Sessionize and output-cache settings.)

**`hasCustomSettings` is a single boolean derived from `OrganisationName`.** Once the editor has set the name, we trust the whole settings object — name, URL, logo. We don't independently check whether each field is filled in. The reasoning: a half-filled `SocialSettings` (name set, URL empty) is an editor mistake worth surfacing as a clearly-wrong schema, not papering over with partial fallbacks. If you want the opposite — per-field fallback chains — replace `hasCustomSettings` with per-field null checks.

**The URL guards via `Uri.TryCreate`.** If an editor pastes a malformed URL into the field, the schema gracefully drops the URL rather than emitting a schema with a bad string. The `Logo` handling is the same: absolute URL preferred, falls back to combining with `baseUri` if the cropper produced a relative one.

**Returns a populated `Organization`, never null.** Every caller can chain straight into `article.Publisher = _organizationSchemaBuilder.Build(socialSettings)` without a null check. That's a small ergonomics win that pays off because *every* schema in the codebase needs a publisher.

### Step 4 — Use the builder from other schema types

[`ArticleSchemaBuilder.Build`](../../../src/UmbracoCommunity.Web/ViewModelBuilders/Schema/ArticleSchemaBuilder.cs) is the canonical consumer:

```csharp
public SchemaNet.Article? Build(IPublishedContent content, SocialSettings? socialSettings)
{
    if (content is not Article articleContent)
    {
        return null;
    }

    var article = new SchemaNet.Article
    {
        Headline = articleContent.Name,
        // …other Article-specific fields…
    };

    // Publisher (organization) — uses tenant-scoped SocialSettings, falls back to Umbraco defaults.
    article.Publisher = _organizationSchemaBuilder.Build(socialSettings);

    return article;
}
```

One line for the publisher. The `ArticleSchemaBuilder` doesn't care whether `socialSettings` is null, doesn't know about the constants, doesn't have to repeat the resolution logic. New schema types follow the same shape — accept `SocialSettings?`, pass it to `OrganizationSchemaBuilder.Build()`, get back a valid `Organization`.

The `WebPage` fallback in `SeoDataService.GetWebPageSchema()` works identically:

```csharp
webPage.Publisher = _organizationSchemaBuilder.Build(socialSettings);
```

### Step 5 — DI registration

All three builders register as scoped services in [`UmbracoBuilderExtensions.AddViewModelBuildersAndDecorators()`](../../../src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs):

```csharp
builder.Services.AddScoped<OrganizationSchemaBuilder>();
builder.Services.AddScoped<ArticleSchemaBuilder>();
builder.Services.AddScoped<BreadcrumbSchemaBuilder>();

builder.Services.AddScoped<ISeoDataService, SeoDataService>();
```

Scoped (per-request) is fine — none of the builders hold per-request state, but they do compose with services that do, so scoped is the safer default.

## Alternatives we considered

- **Throw if `SocialSettings` is missing.** Discussed above as an "obvious fix that doesn't work" — including for completeness. Strictly correct, but the user-visible failure mode (pages 500 on freshly-set-up tenants) is worse than the lower-visibility failure mode of generic-fallback schema.
- **Read fallbacks from `IGlobalSettings` or `appsettings.json`.** Discussed above. Conflates instance defaults with tenant brand. Useful pattern for *single-tenant* sites where there's no distinction; harmful here.
- **Per-field fallback chains.** Instead of "all or nothing" via `hasCustomSettings`, fall back independently on each field: configured name + default URL + configured logo, for example. Doable, more code, mostly papers over editor mistakes that are arguably worth surfacing. We didn't ship it.
- **Skip the schema entirely when `SocialSettings` is null.** Don't emit any `Organization` block at all on tenants that haven't configured one. The downside is the validator output for those pages then says "missing publisher" on every page, which is also noisy SEO-wise. Worse, the article/webpage schema *requires* a publisher to be valid — the schema as a whole would have to be skipped, not just the publisher field. Generic fallback wins for the no-config case.
- **A `IOrganizationProvider` abstraction.** The builder takes a small interface that yields the publisher data, and you implement that interface with whatever resolution strategy you want (per-tenant config, IGlobalSettings, hardcoded, …). Overkill for one resolution strategy. If the codebase grew a second strategy — say, partner sites that get their publisher from a third-party CRM — *then* the abstraction would earn its keep.

## Trade-offs and known limits

- **Fallback "Umbraco" branding is generic.** A freshly-set-up tenant that has a published `Article` will have `"publisher": { "name": "Umbraco", "url": "https://umbraco.com", "logo": "https://umbraco.com/...logo..." }` in its schema. That's valid markup but it's also misleading — search engines will read it as "this article was published by Umbraco the company" rather than the actual tenant. The mitigation is making the editor flow obvious: a "your tenant isn't configured yet" banner in the backoffice, or a deployment checklist that includes filling in `SocialSettings`.
- **No partial fallback.** If `OrganisationName` is set but `OrganisationUrl` is empty, the URL gets dropped (not replaced with the default) and the schema renders with `name = configured`, `url = absent`. That's the all-or-nothing trade-off. Schemas with missing URLs are still valid; this is intentional.
- **Constants live in code.** Changing the fallback "Umbraco" name or logo URL needs a code change. For this site, the fallback is intentionally locked down — editors shouldn't be able to change what new tenants see as their unconfigured-default. For more flexible setups, the constants would lift to options.
- **`SeoDataService` is the only resolver.** Anyone writing a new schema-emitting controller that bypasses `SeoDataService` will need to do the `GetSocialSettings()` call themselves. There's no extension method that wraps "give me a built `Organization` for this content node" — adding one (`currentPage.GetOrganizationSchema(builder)`) would be a small win if more schema-emitting paths show up.
- **Crops are tenant-specific.** `OrganisationLogo.GetCropUrl("logo")` reads a crop named `logo` from the media item. If a tenant uploads a logo but skips configuring that crop in the backoffice, the URL comes back empty and the builder falls back to the default Umbraco logo URL — even though the tenant *has* set a logo. Worth knowing if a tenant's schema is showing the generic logo despite having `OrganisationLogo` populated. Fix: configure the crop, or change the builder to fall back to the uncropped URL when the crop is missing.

## Where to go next

This is the last refinement in the multi-tenant suite. The other refinement built on the same foundation is:

→ [Per-tenant 404 pages with a custom `INotFoundPageResolver`](./per-tenant-404-content-finder.md) — the tenancy resolution problem when there *is* no current page to anchor off because the request 404'd.

For the broader pattern these refinements extend:

→ [Resolving content in a multi-tenant Umbraco site](../foundations/multi-tenant-content-resolution.md)

Hopefully that gives you a clean way of keeping the editor flow forgiving while still emitting structured data that the search engines will actually accept.

---
tags: [primer, content-modelling, document-types, models-builder]
---

# Content modelling primer

Everything editors see in the backoffice — pages, blocks, settings — starts life as an Umbraco content type, and every content type ends up as a generated C# class your controllers, services, and views work against. This primer is about that middle layer: the three shapes a content type can take, how compositions let them share fields without inheritance, how Models Builder turns backoffice configuration into typed code, and the couple of conventions (hand-written partials, `ModelTypeAlias`) that keep the generated layer safe to build on. It's the sibling of the [backend primer](backend.md), which picks up from here and covers what happens *after* you have a typed content model — the view-model-builder pipeline.

> Just want the composition list? Skip to [Compositions: sharing fields without inheritance](#compositions-sharing-fields-without-inheritance).

## Three shapes of content type

Everything in `Models/PublishedModels/` is generated from a backoffice content type, but the backoffice actually lets you create three different *kinds* of thing, and they map onto two different generated base classes:

| Backoffice concept | Generated base class | Constructor takes | Used for |
| --- | --- | --- | --- |
| **Document type** | `PublishedContentModel` | `IPublishedContent` | Pages that live in the content tree and have a URL — `Home`, `ContentPage`, `Article`, `Blog` |
| **Element type** | `PublishedElementModel` | `IPublishedElement` | Content that only exists nested inside something else — a block's properties, or a block's settings |

There's no third base class for "block type" — a block is just an element type that's been added to a Block Grid or Block List data type in the backoffice (see [`BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md) for that wiring). The document-vs-element distinction is the one that actually shows up in code: it decides which base class the generated partial inherits and which content interface (`IPublishedContent` vs `IPublishedElement`) gets passed to its constructor. For example:

```csharp
// Document type — src/UmbracoCommunity.Web/Models/PublishedModels/ContentPage.generated.cs
public partial class ContentPage : PublishedContentModel,
    ICompositionBannerBlock, ICompositionContentBlocks, ICompositionPageConfiguration, ICompositionSeo
{
    public ContentPage(IPublishedContent content, IPublishedValueFallback publishedValueFallback) : base(...)
    ...
}

// Element type used as a block — src/UmbracoCommunity.Web/Models/PublishedModels/TextBlock.generated.cs
public partial class TextBlock : PublishedElementModel
{
    public TextBlock(IPublishedElement content, IPublishedValueFallback publishedValueFallback) : base(...)
    ...
}
```

Both kinds carry a `[PublishedModel("alias")]` attribute and a `ModelTypeAlias` constant regardless of which shape they are — that's the hook the rest of the codebase uses to identify a content type without a magic string (more on that in [Never hardcode a content type alias](#never-hardcode-a-content-type-alias)).

A settings element type (e.g. `SettingsTextBlock`) is structurally identical to a content element type — it's still `PublishedElementModel` — the "settings" label is purely a naming convention for element types that exist to *configure* a block rather than hold its content, and they're the ones you'll see composing the `ISettingsXxx` mixins below.

## Compositions: sharing fields without inheritance

C# doesn't let a class inherit from more than one base, but a backoffice content type can *compose* several others — Umbraco's way of mixing shared fields into unrelated content types. Models Builder turns each composed mixin into a generated interface the content type's class implements, so `ICompositionSeo` (say) is both a backoffice composition and a C# interface with the same shape.

**Page-level compositions** — composed directly onto document types:

| Interface | Adds | Composed onto |
| --- | --- | --- |
| `ICompositionSeo` | `MetaTitle`, `MetaDescription`, `OgImage`, `Robots`, `CustomSchema` | Most pages — `Home`, `ContentPage`, `Article`, `Blog`, `AccountPage`, `SearchPage`, `EventsHome`, `PageNotFound`, `Documentation` |
| `ICompositionPageConfiguration` | `HideFromSearch`, `HideFromSitemap` | Same set as above, plus `OnboardingPage` and `DigitalSignagePage` |
| `ICompositionContentBlocks` | `ContentBlocks` (`BlockGridModel`) | Pages with a general block-grid content area — `Home`, `ContentPage`, `Article`, `PageNotFound`, `DigitalSignagePage` |
| `ICompositionBannerBlock` | `BannerContent` (`BlockGridModel`) — a *separate* block-grid slot from `ContentBlocks`, for hero/banner-only blocks | `Home`, `ContentPage`, `Article`, `PageNotFound`, `Documentation` |

Not every page composes every mixin — `Blog` is a listing page, so it skips `ICompositionContentBlocks` and `ICompositionBannerBlock` entirely; `DigitalSignagePage` skips `ICompositionSeo` and the banner slot because it's a kiosk view with no SEO surface and no chrome (see the Digital Signage section of [CLAUDE.md](../../CLAUDE.md)). Reach for `Services/SeoDataService.cs`'s pattern — `if (currentPage is ICompositionSeo contentModel)` — as the model for consuming a composition: check the *interface*, not a list of document types, so new page types get the behaviour automatically the moment they compose it.

**Block-level compositions** — composed onto element types (content or settings):

| Interface | Adds | Typical use |
| --- | --- | --- |
| `IContentBlockIntro` | `Title`, `Subtitle` | The most common content mixin — 15+ blocks (`CardsBlock`, `SliderBlock`, `TimelineBlock`, `BlogShowcaseBlock`, …) that all open with a heading and subheading |
| `IContentBlockContent` | `Title`, `Content` | Slide/card items that pair a heading with rich text, e.g. `SlideItemBlockWithTag` |
| `IContentBackgroundImage` | `BackgroundImage` | Blocks/items that support a background image, e.g. `SlideItemBlockWithIcon` |
| `ISettingsColour` | `BackgroundColour` | Any block whose background is editor-configurable — pair with `ColourHelper.HasBg()` / `.IsDark()` in the view |
| `ISettingsBlockId` | `BlockId` | Any block that needs an anchor id |
| `ISettingsImage` | `ImageIsCircular` | Image-bearing blocks with a circular-crop toggle |
| `ISettingsButtons` | `ButtonColour` | CTA-link blocks |
| `ISettingsSpeakerGrid` | `SpeakersPerRow` | Sessionize speaker grid blocks |

A settings type composes whichever mix it needs — `SettingsTextBlock` is `ISettingsBlockId, ISettingsColour`; `SettingsImageBlock` is `ISettingsColour, ISettingsImage`. There's no limit on how many mixins one type composes, which is the whole point: `TimelineBlock` and `FormBlock` share nothing except both wanting a title and subtitle, and `IContentBlockIntro` is the entire mechanism that makes that free.

## Models Builder: from backoffice to C#

Every content type change you make in the backoffice needs a matching pass through [Models Builder](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/templating/modelsbuilder) before it exists as C#. The mode differs by environment (`Umbraco:CMS:ModelsBuilder` in `appsettings*.json`):

- **Development** (`appsettings.Development.json` / `appsettings.Local.json`) — `"ModelsMode": "SourceCodeManual"`, writing to `~/../UmbracoCommunity.Web/Models/PublishedModels/` under the `UmbracoCommunity.Web.Models.PublishedModels` namespace, with `"FlagOutOfDateModels": true`.
- **Production** (`appsettings.json`) — `"ModelsMode": "Nothing"`. Production never generates models; it just runs whatever was committed.

`SourceCodeManual` means exactly what it says — **regeneration is a manual step**, not something that happens on save or on build. There's no npm script or `dotnet` task for it; you trigger it from the ModelsBuilder dashboard in the backoffice (Settings section) after changing a document type, element type, or composition. `FlagOutOfDateModels` is what tells you *when* to bother — it surfaces a warning in that same dashboard the moment the backoffice schema and the generated C# have drifted apart. Forget the step and the symptom is always the same: a property you just added in the backoffice doesn't exist on `Model.Content` yet, because the class hasn't caught up.

The generated files are one class per content type — plain partials, safe to overwrite, and not meant to be hand-edited (CLAUDE.md's "auto-generated, do not edit" note applies here). If you need behaviour beyond what's generated, the next section is where that goes instead.

## Extending generated models with hand-written partials

Sometimes a view needs something a content type's raw properties don't give it — a stable id to scope an inline `<style>` block to, or a clamped version of an editor-supplied number. Rather than compute it in the view (where it'd be recomputed every reference) or reach for a service (overkill for a pure presentation value), the convention is a **hand-written partial class** in `Models/ContentModels/`, extending the generated class in the same namespace:

```csharp
// src/UmbracoCommunity.Web/Models/ContentModels/TextBlock.cs
namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class TextBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
```

Thirteen of the fifteen files in `ContentModels/` are exactly this shape — one `IdHash` property, added to whichever block needs to anchor a scoped inline style. `BlogShowcaseBlock.cs` goes a step further and adds `ResolvedNumberOfPostsToShow`, a `switch` expression that clamps the generated `NumberOfPostsToShow` to a sane range (default 3, capped at 12) before any view or service reads it.

The line to hold: **these partials are for presentation-only helpers**, values a Razor view would otherwise have to compute inline. The moment a helper needs to call a service, hit the database, or branch on business rules, it doesn't belong here — it belongs in a proper service in `Services/` (see the [backend primer](backend.md)).

## Never hardcode a content type alias

Every generated model — document or element type — carries a `ModelTypeAlias` constant matching its backoffice alias. The rule, everywhere in this codebase: compare against that constant, never against a string literal.

```csharp
// Good — a doc-type rename is a compile error, not a silent runtime miss
HashSet<string> blogContentTypeAliases = new() { Article.ModelTypeAlias, Blog.ModelTypeAlias };

// Bad — "article" typo'd, or the alias renamed, and this silently stops matching
HashSet<string> blogContentTypeAliases = new() { "article", "blog" };
```

This shows up everywhere a content type needs identifying rather than casting: `BlogContentCacheInvalidationHandler` gating which published/unpublished events should evict the output cache, `PublishedContentExtensions` walking up to find a tenant's `Settings`/`NavigationSettings`/`SocialSettings` nodes, `OnboardingRedirectMiddleware` checking whether the current node is the `OnboardingPage`, `BlogFolderRedirectMiddleware` matching `BlogYearFolder`/`BlogMonthFolder` — and it applies in Razor too, not just C#, e.g. `Views/Partials/Blocks/CardsBlock.cshtml` comparing `c.Content.ContentType.Alias == ImageBlock.ModelTypeAlias` when a block needs to render differently per nested content type. See [`CODE_CONVENTIONS.md`](../../CODE_CONVENTIONS.md) for the canonical good/bad example.

## The `Models/` folder map

Content modelling touches three of the subfolders under `src/UmbracoCommunity.Web/Models/` directly; the rest belong to the view side the [backend primer](backend.md) covers.

| Folder | What's in it |
| --- | --- |
| `PublishedModels/` | Generated document/element types and compositions — **auto-generated, don't hand-edit** |
| `ContentModels/` | Hand-written partials extending generated models with presentation-only helpers (previous section) |
| `Pages/` | Page-level view models (`HomePageViewModel`, `ArticlePageViewModel`, …), including a `Documentation/` subfolder for the documentation-viewer's own view models |
| `Api/`, `ServiceModels/`, `ViewModels/{Blocks,Components,Properties}/` | DTOs, service-layer models, and component/block view models — see the [backend primer](backend.md) for how these fit into the request pipeline |

## From content model to view model

Everything above gets you a typed `IPublishedContent`-backed model — `currentPage.As<ContentPage>()`, say. That's not yet what a Razor view renders against for a *page*: a view-model-builder converts the content model into a view-shaped model first, decoupling the view from Umbraco's content API. (Blocks skip this step entirely and bind the content model directly — see [`BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md).) That pipeline — `IViewModelBuilder<T>`, `PageViewModelBase`, where builders get registered — is the backend primer's territory; see its [view model builder pattern](backend.md#the-view-model-builder-pattern) section for the rest of the journey.

The SEO composition deserves the same signpost: `ICompositionSeo` is where a page becomes SEO-capable, but the pipeline that turns it into meta tags, Open Graph data, and Schema.NET structured data is bigger than one composition — see the [SEO and structured data primer](seo-and-structured-data.md) for that in full.

## Where to go next

- **[`docs/BUILDING_PAGES.md`](../BUILDING_PAGES.md)** — creating a new document type end to end: doc type → controller → view model → builder → view.
- **[`docs/BUILDING_BLOCKS.md`](../BUILDING_BLOCKS.md)** — creating a new element type as a block: element type → optional settings type → optional content-model partial → view.
- **[`CODE_CONVENTIONS.md`](../../CODE_CONVENTIONS.md)** — the `ModelTypeAlias` rule and view-model naming conventions in full.
- **[Backend primer](backend.md)** — what happens after you have a typed content model: the view-model-builder pipeline, controllers, and services.

Hopefully that's enough to make sense of `Models/PublishedModels/` the next time it shows up in a diff — welcome aboard!

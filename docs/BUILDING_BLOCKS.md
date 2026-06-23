# Building Blocks

Step-by-step instructions for adding a content block in this solution, following the current pattern.

> **No view models, no builders.** Block views bind Umbraco's `BlockGridItem<TContent, TSettings>` directly to the content and settings models — there is no per-block `…ViewModel` or `…ViewModelBuilder`. (Earlier versions of this guide described a `BlockViewModelBuilderBase` pattern; that architecture no longer exists. View models and builders are for *pages*, not blocks — see [`BUILDING_PAGES.md`](BUILDING_PAGES.md).)

## Overview

A block is made of:

1. **Element type** — the block's content properties (created in the backoffice).
2. **Settings type** *(optional)* — configuration, usually composed from shared mixins (`ISettingsColour`, `ISettingsBlockId`, …).
3. **Content model** — the Models Builder published model. Optionally extended by a hand-written partial in `Models/ContentModels/` that adds *view-only* helpers (an `IdHash`, a clamped count, etc.).
4. **View** — a Razor partial at `Views/Partials/Blocks/{Alias}.cshtml` inheriting `BlockGridItem<TContent, TSettings>`.
5. **CSS** *(optional)* — `StaticAssets/src/css/blocks/{block}.css`, imported into the blocks index.

There's **no C# registration step** — a block renders by convention (matching filename) once its element type is added to a Block Grid / Block List data type.

## Step-by-step

### Step 1 — Create the element type

In **Settings → Document Types**, create an *element type* (not a document type) and add the properties the block needs. If it should be configurable, also create a matching `Settings…` element type (Step 2).

### Step 2 — Compose settings from the shared mixins (optional)

Settings types are element types too, and they usually pick up shared behaviour by composing one or more of these (defined as Models Builder mixin models / interfaces):

- **`ISettingsColour`** — adds `BackgroundColour`. Pair with the `ColourHelper` extensions `HasBg()` / `IsDark()` in the view.
- **`ISettingsBlockId`** — adds `BlockId` for an anchor target on the rendered element.
- **`IContentBlockIntro`** — adds `Title` / `Subtitle` to a *content* type (used by intro-bearing blocks like `BlogShowcaseBlock`).

A block that needs no configuration simply has no `Settings…` type — its view omits the second generic parameter (`BlockGridItem<HeroBanner>`).

### Step 3 — Regenerate Models Builder classes

Models Builder runs in **SourceCodeManual** mode (development), so it does **not** regenerate automatically — trigger it manually after changing element types. It emits partials into `Models/PublishedModels/` (e.g. `TextBlock`, `SettingsTextBlock`).

### Step 4 — Add a content-model partial for view helpers (optional)

When the view needs a helper that isn't a content property, add a partial class in `Models/ContentModels/` in the `…PublishedModels` namespace. The commonest is a stable per-instance id for anchoring inline `<style>`:

```csharp
// src/UmbracoCommunity.Web/Models/ContentModels/TextBlock.cs
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class TextBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}
```

Computed/clamped values live here too — e.g. `BlogShowcaseBlock.cs` adds `ResolvedNumberOfPostsToShow` (defaults to 3, capped at 12) on top of the generated `NumberOfPostsToShow` property. Keep these partials to *presentation* helpers; business logic belongs in a service (Step 6).

### Step 5 — Create the view

Add `Views/Partials/Blocks/{Alias}.cshtml` (filename = content type alias, PascalCase), inheriting `BlockGridItem<TContent, TSettings>`. Bind `Model.Content.*` and `Model.Settings.*` directly:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.BlockGridItem<TextBlock, SettingsTextBlock>>

@if (Model.Content.Text != null)
{
    bool hasBg = Model.Settings.HasBg();        // ColourHelper extension on ISettingsColour
    bool bgIsDark = hasBg && Model.Settings.IsDark();

    <div class="dc-text-block @(hasBg ? "has-bg" : "transparent") @(bgIsDark ? "bg-dark" : "")" id="@Model.Content.IdHash">
        @if (hasBg)
        {
            <style asp-add-nonce="true">
                #@Model.Content.IdHash { --block-background-color: @Model.Settings?.BackgroundColour?.Color; }
            </style>
        }
        <div class="dc-text-block__content" id="@Model.Settings?.BlockId">
            @Html.Raw(Model.Content.Text.ToHtmlString())
        </div>
    </div>
}
```

Note the recurring idioms: the `IdHash` scopes an inline `asp-add-nonce` `<style>` block (the nonce keeps it CSP-compliant), rich text renders via `@Html.Raw(...ToHtmlString())`, and `BlockId` becomes an anchor target.

### Step 6 — Inject services for content-driven blocks

A block that needs data beyond its own properties injects a service into the view with `@inject` and resolves against the current page (`Umbraco.AssignedContentItem`). `BlogShowcaseBlock` is the model:

```cshtml
@inherits Umbraco.Cms.Web.Common.Views.UmbracoViewPage<Umbraco.Cms.Core.Models.Blocks.BlockGridItem<BlogShowcaseBlock, SettingsBlogShowcaseBlock>>
@using UmbracoCommunity.Web.Services
@inject IBlogService BlogService;

@{
    var articles = BlogService.GetRecentArticles(
        Umbraco.AssignedContentItem,
        Model.Content.BlogCategoryFilter?.Select(c => c.Key),
        Model.Content.BlogTagFilter,
        Model.Content.ResolvedNumberOfPostsToShow).ToList();
}
```

The service itself is a normal scoped service registered in `Services/` — see the [backend primer](primers/backend.md).

### Step 7 — Add styling (optional)

Create `src/UmbracoCommunity.StaticAssets/src/css/blocks/{block}.css` and import it from the blocks index stylesheet. Follow the `.dc-{block}` class convention and drive colours from the CSS custom property the view sets (`--block-background-color`).

### Step 8 — Wire it up (backoffice, not code)

There is no DI registration. Add the new element type to the relevant **Block Grid** / **Block List** data type in the backoffice (or, for a restricted editor, to the allowed set — see [Block Restrictions](../src/UmbracoCommunity.BlockRestrictions/README.md)). At render time Umbraco resolves `Views/Partials/Blocks/{Alias}.cshtml` by convention; backoffice **preview** resolves through the `BlockPreview` `ViewLocations` in `appsettings.json`, which fall back to the same partial.

### Step 9 — Tests (where there's logic to test)

There's no per-block builder to unit-test any more. Test the *service* a block depends on (e.g. `BlogService`) and any shared helper (e.g. `ColourHelper`); a view with no logic beyond binding needs no test.

## Common patterns

- **Colour theming** — compose `ISettingsColour`, then `Model.Settings.HasBg()` / `.IsDark()` (`Helpers/ColourHelper.cs`) decide the CSS classes; the colour is pushed into a scoped `--block-background-color` custom property.
- **Anchor id** — compose `ISettingsBlockId`; render `id="@Model.Settings?.BlockId"`.
- **Images** — `MediaWithCrops.GetCropUrl("desktop2x")` for `<picture>`/`srcset`, or inject `IImageUrlBuilder` for signed crop URLs (`HeroBanner.cshtml`).
- **Links / CTAs** — Umbraco's `Link` model (`.Url`, `.Target`, `.Name`); the `RenderButtonCTA` HTML helper in `Extensions/HtmlHelperExtensions.cs` for themed buttons.
- **Container / nested blocks** — a property typed `BlockListModel` (or nested Block Grid); `Extensions/PublishedElementExtensions.cs` (`ParseBlockGrid`, `GetRows`) helps walk them. `SliderBlock` and `CardWithImageAndTextBlock` are examples.

## Examples

Real blocks to read for reference (content model + optional settings + view, all under `Views/Partials/Blocks/`):

- **TextBlock** — `TextBlock` / `SettingsTextBlock`; rich text with optional background colour/image. Content partial adds `IdHash`.
- **CallToActionBlock** — `CallToActionBlock` / `SettingsCallToActionBlock`; title, body, a `Link` CTA, optional background image.
- **CardWithImageAndTextBlock** — `CardWithImageAndTextBlock` / `SettingsCardWithImageAndTextBlock`; image + text card, nested via a `BlockListModel`.
- **ImageBlock** — `ImageBlock` / `SettingsImageBlock`; `GetCropUrl` image with circular/colour options.
- **BlogShowcaseBlock** — service-driven (`@inject IBlogService`); grid/slider toggle via settings; reuses the `dc-slider` web component.
- **SliderBlock** — container block holding nested `SlideItemBlockWithTag` / `SlideItemBlockWithIcon` items.

## Troubleshooting

1. **Block not offered in the editor** — its element type isn't added to the Block Grid/List data type (or isn't in a restricted editor's allowed set).
2. **View not found** — the partial filename must match the content type alias (PascalCase) under `Views/Partials/Blocks/`.
3. **Backoffice preview blank** — check the `BlockPreview` `ViewLocations` in `appsettings.json`.
4. **A property is missing on `Model.Content`** — regenerate Models Builder (Step 3).
5. **Background colour / CSS not applying** — confirm the view emits the `--block-background-color` custom property and the CSS reads it.

For the page equivalent (which *does* use view models and builders), see [`BUILDING_PAGES.md`](BUILDING_PAGES.md). For why specific blocks are shaped the way they are, see the [tutorials suite](tutorials/README.md).
</content>

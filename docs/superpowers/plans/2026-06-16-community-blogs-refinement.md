# Community Blogs Refinement Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a "featured" mode + "Read more" link to the Community Blog Posts block, restyle its full-listing pager to match the regular blog, and ingest the 60 cached community posts into Examine so they appear (marked as external) in site search.

**Architecture:** The existing `CommunityBlogPostsBlock` (SSR Block Grid, server-paginated over `ICommunityBlogsService.GetPage`) gains a `featuredOnly` toggle, a `numberOfFeaturedPosts` count, and a `readMoreLink`. A new dedicated Examine index `CommunityBlogsIndex` is rebuilt on every feed refresh by a `CommunityBlogsSearchIndexer`; `SearchService` queries it alongside the content index and merges results by score. External results are rendered in `SearchPage.cshtml` with a new-tab link + an "external" indicator.

**Tech Stack:** ASP.NET Core / Umbraco CMS 17, Examine (Lucene.NET), Models Builder (SourceCodeManual), Umbraco Deploy `.uda` artifacts, PostCSS, xUnit + Moq (test project).

**Reference spec:** `docs/superpowers/specs/2026-06-16-community-blogs-refinement-design.md`

**Branch:** Create a feature branch off `develop` before starting (see Task 0).

---

## Known facts (verified, do not re-derive)

- Settings element type `settingsCommunityBlogPostsBlock` — `.uda`: `src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__527ea1d78b3046299695b2f75e44448a.uda`. Currently has one property `postsPerPage` (integer). Generated model: `src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs`.
- Content element type `communityBlogPostsBlock` — `.uda`: `src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__9b898ccfe50346108e62b5348baa88e4.uda`. Currently has NO own properties (Title/Subtitle come from composed `IContentBlockIntro`). Generated model: `src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs`.
- Data type UDIs to reuse:
  - Integer (`Umbraco.Integer`): `umb://data-type/2e6d3631066e44b8aec496f09099b2b5`
  - On/Off Toggle (`Umbraco.TrueFalse`): `umb://data-type/d0708ebfa17a4b1792b42ffbf9962c8d`
  - Url picker (max 1) (`Umbraco.MultiUrlPicker`): `umb://data-type/0bdffabfddf745c5a93bc0c66930f43d` → generates a single `global::Umbraco.Cms.Core.Models.Link`.
- New property keys to use (pre-minted GUIDs):
  - `featuredOnly`: `bfa1f240-35f1-4f16-b300-fcececf0c105`
  - `numberOfFeaturedPosts`: `ef58c966-85c4-403f-b7c3-805fc2f51142`
  - `readMoreLink`: `dd6d7f1a-d716-4a36-9abe-a0d9bb2900b7`
- Block partial view: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml`. Block CSS: `src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css`.
- Search: `src/UmbracoCommunity.Web/Services/SearchService.cs`, view `src/UmbracoCommunity.Web.UI/Views/SearchPage.cshtml`, view model `src/UmbracoCommunity.Web/Models/Pages/SearchPageViewModel.cs` (contains `SearchResultItem`).
- Feed wiring composer: `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs`. Cache/refresh service: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsService.cs`. Test project: `src/UmbracoCommunity.Web.Tests/` (existing `CommunityBlogs/CommunityBlogsServiceTests.cs`, `CommunityBlogsTestHelpers.cs`).
- The block grid CSS already renders 3 columns at the `--lg` breakpoint — no grid change needed for the full listing.

---

## Task 0: Feature branch

- [ ] **Step 1: Create branch off develop**

```bash
cd /home/sebastiaan/Dev/Umbraco.Community.Site
git fetch origin
git checkout -b feat/community-blogs-refinement origin/develop
```

Expected: on a new branch `feat/community-blogs-refinement`.

---

## Task 1: Block properties — `featuredOnly`, `numberOfFeaturedPosts`, `readMoreLink`

Adds the two settings properties and the content link property, via Deploy `.uda` edits + matching Models Builder partials. (Models Builder is in SourceCodeManual mode — the generated files are edited by hand here; the `.uda` keeps Deploy/backoffice in sync.)

**Files:**
- Modify: `src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__527ea1d78b3046299695b2f75e44448a.uda`
- Modify: `src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__9b898ccfe50346108e62b5348baa88e4.uda`
- Modify: `src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs`
- Modify: `src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs`

- [ ] **Step 1: Add the two settings properties to the settings `.uda`**

In `document-type__527ea1d78b3046299695b2f75e44448a.uda`, replace the `"PropertyTypes"` array with the three entries below (existing `postsPerPage` kept), and add the toggle data-type dependency to `"Dependencies"`.

```json
  "PropertyTypes": [
    {
      "Key": "bfa1f240-35f1-4f16-b300-fcececf0c105",
      "Alias": "featuredOnly",
      "DataType": "umb://data-type/d0708ebfa17a4b1792b42ffbf9962c8d",
      "ValueType": "System.Boolean",
      "Name": "Show featured posts only (no paging)",
      "Description": "When on, shows the latest posts as a teaser with a 'Read more' link and no pagination."
    },
    {
      "Key": "ef58c966-85c4-403f-b7c3-805fc2f51142",
      "Alias": "numberOfFeaturedPosts",
      "DataType": "umb://data-type/2e6d3631066e44b8aec496f09099b2b5",
      "ValueType": "System.Int32",
      "Name": "Number of featured posts",
      "Description": "How many posts to show in featured mode (default 6, max 12)."
    },
    {
      "Key": "ffdcb710-1567-4ff0-a306-1d397dc2f46c",
      "Alias": "postsPerPage",
      "DataType": "umb://data-type/2e6d3631066e44b8aec496f09099b2b5",
      "ValueType": "System.Int32",
      "Name": "Posts Per Page"
    }
  ],
```

Then add to the `"Dependencies"` array (alongside the existing ones) a dependency on the toggle data type:

```json
    {
      "Udi": "umb://data-type/d0708ebfa17a4b1792b42ffbf9962c8d",
      "Ordering": true
    },
```

- [ ] **Step 2: Add the `readMoreLink` property to the content `.uda`**

In `document-type__9b898ccfe50346108e62b5348baa88e4.uda`, set `"PropertyTypes"` to:

```json
  "PropertyTypes": [
    {
      "Key": "dd6d7f1a-d716-4a36-9abe-a0d9bb2900b7",
      "Alias": "readMoreLink",
      "DataType": "umb://data-type/0bdffabfddf745c5a93bc0c66930f43d",
      "ValueType": "Umbraco.MultiUrlPicker",
      "Name": "Read more link",
      "Description": "Shown only in featured mode. Links to the full community blog listing page."
    }
  ],
```

And add to its `"Dependencies"` array:

```json
    {
      "Udi": "umb://data-type/0bdffabfddf745c5a93bc0c66930f43d",
      "Ordering": true
    },
```

- [ ] **Step 3: Add the settings properties to the Models Builder partial**

In `SettingsCommunityBlogPostsBlock.generated.cs`, add these two properties inside the class, just before the existing `PostsPerPage` property:

```csharp
		///<summary>
		/// Show featured posts only (no paging)
		///</summary>
		[global::System.CodeDom.Compiler.GeneratedCodeAttribute("Umbraco.ModelsBuilder.Embedded", "")]
		[ImplementPropertyType("featuredOnly")]
		public virtual bool FeaturedOnly => this.Value<bool>(_publishedValueFallback, "featuredOnly");

		///<summary>
		/// Number of featured posts
		///</summary>
		[global::System.CodeDom.Compiler.GeneratedCodeAttribute("Umbraco.ModelsBuilder.Embedded", "")]
		[ImplementPropertyType("numberOfFeaturedPosts")]
		public virtual int NumberOfFeaturedPosts => this.Value<int>(_publishedValueFallback, "numberOfFeaturedPosts");
```

- [ ] **Step 4: Add the `ReadMoreLink` property to the content Models Builder partial**

`CommunityBlogPostsBlock.generated.cs` may not exist as a hand-editable partial yet (the IdHash partial lives in `Models/ContentModels/CommunityBlogPostsBlock.cs`). Open `src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs` and add this property inside the `CommunityBlogPostsBlock` class body:

```csharp
		///<summary>
		/// Read more link
		///</summary>
		[global::System.CodeDom.Compiler.GeneratedCodeAttribute("Umbraco.ModelsBuilder.Embedded", "")]
		[global::System.Diagnostics.CodeAnalysis.MaybeNull]
		[ImplementPropertyType("readMoreLink")]
		public virtual global::Umbraco.Cms.Core.Models.Link ReadMoreLink => this.Value<global::Umbraco.Cms.Core.Models.Link>(_publishedValueFallback, "readMoreLink");
```

(If the generated file already has a `_publishedValueFallback` field and `ImplementPropertyType` usings like the sibling generated models, this compiles as-is. Match the existing usings in that file.)

- [ ] **Step 5: Build to verify the models compile**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__527ea1d78b3046299695b2f75e44448a.uda \
        src/UmbracoCommunity.Web.UI/umbraco/Deploy/Revision/document-type__9b898ccfe50346108e62b5348baa88e4.uda \
        src/UmbracoCommunity.Web/Models/PublishedModels/SettingsCommunityBlogPostsBlock.generated.cs \
        src/UmbracoCommunity.Web/Models/PublishedModels/CommunityBlogPostsBlock.generated.cs
git commit -m "feat(community-blogs): add featuredOnly, numberOfFeaturedPosts, readMoreLink block properties"
```

---

## Task 2: Featured-mode rendering + "Read more" link

In featured mode the block shows the latest N posts (no pager) and a right-aligned "Read more" text link. Full mode is unchanged (pager handled in Task 3).

**Files:**
- Modify: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml`
- Modify: `src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css`

- [ ] **Step 1: Compute mode + page size in the view's `@{ }` block**

In `CommunityBlogPostsBlock.cshtml`, replace the page-size/`requestedPage`/`result` setup (the lines computing `settingsPerPage`, `pageSize`, `requestedPage`, and `result = CommunityBlogsService.GetPage(...)`) with:

```csharp
    const int DefaultPostsPerPage = 12;
    const int DefaultFeaturedPosts = 6;
    const int MaxFeaturedPosts = 12;

    var featuredOnly = Model.Settings?.FeaturedOnly ?? false;

    int requestedPage = 1;
    PagedCommunityBlogPosts result;
    if (featuredOnly)
    {
        var featuredCount = Model.Settings?.NumberOfFeaturedPosts ?? 0;
        if (featuredCount <= 0) featuredCount = DefaultFeaturedPosts;
        featuredCount = Math.Clamp(featuredCount, 1, MaxFeaturedPosts);
        result = CommunityBlogsService.GetPage(1, featuredCount);
    }
    else
    {
        var settingsPerPage = Model.Settings?.PostsPerPage ?? 0;
        var pageSize = settingsPerPage > 0 ? settingsPerPage : DefaultPostsPerPage;
        if (int.TryParse(Context.Request.Query["page"], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
        {
            requestedPage = parsed;
        }
        result = CommunityBlogsService.GetPage(requestedPage, pageSize);
    }
```

- [ ] **Step 2: Gate the pager on full mode and render the Read more link in featured mode**

Replace the existing `@if (result.TotalPages > 1) { <nav ...> ... </nav> }` block with the conditional below. In full mode it keeps the pager (Task 3 restyles the inner markup); in featured mode it renders the Read more link instead.

```cshtml
        @if (!featuredOnly && result.TotalPages > 1)
        {
            @* pager markup — see Task 3 *@
            <partial name="Blocks/_CommunityBlogsPager" model="result" view-data='new ViewDataDictionary(ViewData) { ["anchor"] = anchor }' />
        }

        @if (featuredOnly && Model.Content.ReadMoreLink is { } readMore && !string.IsNullOrEmpty(readMore.Url))
        {
            <div class="dc-community-blogs__cta">
                <a href="@readMore.Url" target="@(string.IsNullOrEmpty(readMore.Target) ? null : readMore.Target)">
                    @(string.IsNullOrWhiteSpace(readMore.Name) ? "Read more on the blog" : readMore.Name)
                </a>
            </div>
        }
```

> NOTE: To avoid introducing a partial, you MAY instead keep the pager markup inline here. If you keep it inline, do Task 3's restyle directly in this `if` block and drop the `<partial>` line. Decide once and be consistent. The remaining steps assume inline markup (no separate partial) — so use the inline pager and apply Task 3 here.

Simplest concrete form (inline, no partial) — replace the whole pager `@if` with:

```cshtml
        @if (!featuredOnly && result.TotalPages > 1)
        {
            <nav class="dc-community-blogs__pagination" aria-label="Community blog posts pages">
                @* Task 3 fills in the First/Prev/Page X of Y/Next/Last markup here *@
            </nav>
        }

        @if (featuredOnly && Model.Content.ReadMoreLink is { } readMore && !string.IsNullOrEmpty(readMore.Url))
        {
            <div class="dc-community-blogs__cta">
                <a href="@readMore.Url" target="@(string.IsNullOrEmpty(readMore.Target) ? null : readMore.Target)">
                    @(string.IsNullOrWhiteSpace(readMore.Name) ? "Read more on the blog" : readMore.Name)
                </a>
            </div>
        }
```

- [ ] **Step 3: Add the CTA style**

Append to `community-blogs-block.css`:

```css
/* "Read more" CTA (featured mode) — right-aligned plain text link;
   picks up the global #main-content a:not(.btn) pink-underline animation. */
.dc-community-blogs__cta {
  margin-top: var(--unit-md);
  text-align: right;
}

.dc-community-blogs.bg-dark .dc-community-blogs__cta a {
  color: var(--color-white);
}
```

- [ ] **Step 4: Build the frontend assets**

Run: `cd src/UmbracoCommunity.StaticAssets && npm run build`
Expected: build completes, `community-blogs-block.css` rebuilt into `dist/`.

- [ ] **Step 5: Manual verification**

Run the backend (`cd src/UmbracoCommunity.Web.UI && dotnet run`) and the Vite dev server. On a page with the block:
- Toggle `Show featured posts only` ON, set `Number of featured posts` = 6, pick a Read more link → expect 6 cards, no pager, a right-aligned "Read more" link.
- Toggle OFF → expect the paged listing (pager restyled after Task 3).
Expected: matches Image #12 (featured) behaviour.

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml \
        src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css
git commit -m "feat(community-blogs): featured mode with Read more link"
```

---

## Task 3: Pager restyle to match the regular blog / search

Replace the numbered pager with the First / Previous / `Page X of Y` / Next / Last pattern (mirrors `SearchPage.cshtml`).

**Files:**
- Modify: `src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml`
- Modify: `src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css`

- [ ] **Step 1: Fill in the pager `<nav>` markup**

Inside the `@if (!featuredOnly && result.TotalPages > 1)` `<nav>` from Task 2, put:

```cshtml
                @if (result.Page > 1)
                {
                    <a class="dc-community-blogs__page-link" rel="first" href="@BuildPageUrl(1, anchor)">First</a>
                    <a class="dc-community-blogs__page-link" rel="prev" href="@BuildPageUrl(result.Page - 1, anchor)">Previous</a>
                }
                else
                {
                    <span class="dc-community-blogs__page-link is-disabled" aria-disabled="true">First</span>
                    <span class="dc-community-blogs__page-link is-disabled" aria-disabled="true">Previous</span>
                }

                <span class="dc-community-blogs__page-current" aria-current="page">Page @result.Page of @result.TotalPages</span>

                @if (result.Page < result.TotalPages)
                {
                    <a class="dc-community-blogs__page-link" rel="next" href="@BuildPageUrl(result.Page + 1, anchor)">Next</a>
                    <a class="dc-community-blogs__page-link" rel="last" href="@BuildPageUrl(result.TotalPages, anchor)">Last</a>
                }
                else
                {
                    <span class="dc-community-blogs__page-link is-disabled" aria-disabled="true">Next</span>
                    <span class="dc-community-blogs__page-link is-disabled" aria-disabled="true">Last</span>
                }
```

- [ ] **Step 2: Update pager CSS — remove `.is-current` numbered styling, add `.is-disabled` + `.dc-community-blogs__page-current`**

In `community-blogs-block.css`, replace the `.dc-community-blogs__page-link.is-current { ... }` rule with:

```css
.dc-community-blogs__page-link.is-disabled {
  opacity: 0.5;
  pointer-events: none;
}

.dc-community-blogs__page-current {
  display: inline-flex;
  align-items: center;
  padding: 0.4rem 0.75rem;
  font-weight: 600;
  color: var(--color-dark-grey);
}

.dc-community-blogs.bg-dark .dc-community-blogs__page-current {
  color: var(--color-white);
}
```

- [ ] **Step 3: Build frontend assets**

Run: `cd src/UmbracoCommunity.StaticAssets && npm run build`
Expected: completes without error.

- [ ] **Step 4: Manual verification**

With featured mode OFF and >1 page of posts, confirm the pager reads `First Previous Page X of Y Next Last`, with First/Previous disabled on page 1 and Next/Last disabled on the last page. Matches the regular blog/search pager.

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/CommunityBlogPostsBlock.cshtml \
        src/UmbracoCommunity.StaticAssets/src/css/blocks/community-blogs-block.css
git commit -m "feat(community-blogs): restyle full-listing pager to match the blog/search"
```

---

## Task 4: `CommunityBlogsSearchIndexer` (+ ValueSet mapping, TDD)

A component that converts `CommunityBlogsData` into Examine `ValueSet`s and rebuilds the `CommunityBlogsIndex`. The pure mapping is unit-tested; the actual index write is exercised via a fake `IIndex`.

**Files:**
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/ICommunityBlogsIndexer.cs`
- Create: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsSearchIndexer.cs`
- Test: `src/UmbracoCommunity.Web.Tests/CommunityBlogs/CommunityBlogsSearchIndexerTests.cs`

- [ ] **Step 1: Write the failing test for ValueSet mapping**

Create `CommunityBlogsSearchIndexerTests.cs`:

```csharp
using Examine;
using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;
using Xunit;

namespace UmbracoCommunity.Web.Tests.CommunityBlogs;

public class CommunityBlogsSearchIndexerTests
{
    [Fact]
    public void BuildValueSets_MapsPostFieldsAndKeysById()
    {
        var post = new CommunityBlogPost(
            Id: "post-1",
            Title: "Hello Umbraco",
            Url: "https://example.com/hello",
            Excerpt: "An excerpt",
            CoverImageUrl: null,
            PublishedAt: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            AuthorName: "Jane Doe",
            AuthorAvatarUrl: null,
            AuthorProfileUrl: null);

        var data = new CommunityBlogsData(new[] { post }, DateTimeOffset.UtcNow);

        var sets = CommunityBlogsSearchIndexer.BuildValueSets(data).ToList();

        Assert.Single(sets);
        var vs = sets[0];
        Assert.Equal("post-1", vs.Id);
        Assert.Equal(CommunityBlogsSearchIndexer.Category, vs.Category);
        Assert.Equal("Hello Umbraco", vs.Values["title"].Single());
        Assert.Equal("An excerpt", vs.Values["excerpt"].Single());
        Assert.Equal("Jane Doe", vs.Values["author"].Single());
        Assert.Equal("https://example.com/hello", vs.Values["url"].Single());
    }
}
```

- [ ] **Step 2: Run the test — verify it fails to compile**

Run: `dotnet test src/UmbracoCommunity.Web.Tests --filter CommunityBlogsSearchIndexerTests`
Expected: FAIL — `CommunityBlogsSearchIndexer` does not exist.

- [ ] **Step 3: Create the interface**

`ICommunityBlogsIndexer.cs`:

```csharp
namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public interface ICommunityBlogsIndexer
{
    /// <summary>Rebuilds the community blogs Examine index from the given data (clears, then re-adds).</summary>
    void Rebuild(CommunityBlogsData data);
}
```

- [ ] **Step 4: Implement `CommunityBlogsSearchIndexer`**

`CommunityBlogsSearchIndexer.cs`:

```csharp
using System.Globalization;
using Examine;
using Microsoft.Extensions.Logging;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

public sealed class CommunityBlogsSearchIndexer : ICommunityBlogsIndexer
{
    public const string IndexName = "CommunityBlogsIndex";
    public const string Category = "communityBlogPost";

    private readonly IExamineManager _examineManager;
    private readonly ILogger<CommunityBlogsSearchIndexer> _logger;

    public CommunityBlogsSearchIndexer(IExamineManager examineManager, ILogger<CommunityBlogsSearchIndexer> logger)
    {
        _examineManager = examineManager;
        _logger = logger;
    }

    public static IEnumerable<ValueSet> BuildValueSets(CommunityBlogsData data)
    {
        foreach (var post in data.Posts)
        {
            var values = new Dictionary<string, object>
            {
                ["title"] = post.Title ?? string.Empty,
                ["excerpt"] = post.Excerpt ?? string.Empty,
                ["author"] = post.AuthorName ?? string.Empty,
                ["url"] = post.Url ?? string.Empty,
                ["publishedAt"] = post.PublishedAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture),
            };
            yield return new ValueSet(post.Id, Category, values);
        }
    }

    public void Rebuild(CommunityBlogsData data)
    {
        if (!_examineManager.TryGetIndex(IndexName, out var index))
        {
            _logger.LogWarning("Community blogs index '{Index}' not found; skipping indexing.", IndexName);
            return;
        }

        try
        {
            // Small, fully-replaced set: wipe and re-add so removed posts don't linger.
            index.CreateIndex();
            index.IndexItems(BuildValueSets(data));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to rebuild community blogs index.");
        }
    }
}
```

- [ ] **Step 5: Run the test — verify it passes**

Run: `dotnet test src/UmbracoCommunity.Web.Tests --filter CommunityBlogsSearchIndexerTests`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/ICommunityBlogsIndexer.cs \
        src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsSearchIndexer.cs \
        src/UmbracoCommunity.Web.Tests/CommunityBlogs/CommunityBlogsSearchIndexerTests.cs
git commit -m "feat(community-blogs): add Examine indexer + ValueSet mapping"
```

---

## Task 5: Register the index + rebuild on refresh

Register `CommunityBlogsIndex` with Examine and invoke the indexer from `CommunityBlogsService.RefreshAsync`.

**Files:**
- Modify: `src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs`
- Modify: `src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsService.cs`
- Modify: `src/UmbracoCommunity.Web.Tests/CommunityBlogs/CommunityBlogsServiceTests.cs`

- [ ] **Step 1: Write the failing test — refresh invokes the indexer**

In `CommunityBlogsServiceTests.cs`, add a test that constructs the service with a mocked `ICommunityBlogsIndexer` and asserts `Rebuild` is called once with the refreshed data when aggregation returns data, and never when it returns null. Match the existing constructor wiring/helpers in that file (use `CommunityBlogsTestHelpers` as the other tests do).

```csharp
[Fact]
public async Task RefreshAsync_WhenDataAggregated_RebuildsSearchIndex()
{
    // Arrange: use the same aggregator/downloader/options/cache setup the other
    // tests in this file use; supply a Mock<ICommunityBlogsIndexer> as the new dependency.
    var indexer = new Mock<ICommunityBlogsIndexer>();
    var service = CommunityBlogsTestHelpers.CreateService(/* existing args */, indexer.Object);

    // Act
    await service.RefreshAsync();

    // Assert
    indexer.Verify(i => i.Rebuild(It.Is<CommunityBlogsData>(d => d.Posts.Count > 0)), Times.Once);
}

[Fact]
public async Task RefreshAsync_WhenNoData_DoesNotRebuildSearchIndex()
{
    var indexer = new Mock<ICommunityBlogsIndexer>();
    var service = CommunityBlogsTestHelpers.CreateService(/* aggregator returning null */, indexer.Object);

    await service.RefreshAsync();

    indexer.Verify(i => i.Rebuild(It.IsAny<CommunityBlogsData>()), Times.Never);
}
```

> Adapt the construction to the actual helper signature in `CommunityBlogsTestHelpers` / the existing tests. If the helper builds the service directly, add an `ICommunityBlogsIndexer` parameter (defaulting to `Mock.Of<ICommunityBlogsIndexer>()`) so existing tests keep compiling.

- [ ] **Step 2: Run the test — verify it fails**

Run: `dotnet test src/UmbracoCommunity.Web.Tests --filter CommunityBlogsServiceTests`
Expected: FAIL — `CommunityBlogsService` has no `ICommunityBlogsIndexer` dependency yet.

- [ ] **Step 3: Inject the indexer into `CommunityBlogsService` and call it in `RefreshAsync`**

In `CommunityBlogsService.cs`: add a `private readonly ICommunityBlogsIndexer _indexer;` field, add it as the last constructor parameter, assign it. In `RefreshAsync`, after `data = await _imageDownloader.LocalizeAsync(...)` and the cache writes (before/after `WriteCacheFileAsync` is fine — put it right after setting the caches), add:

```csharp
        _indexer.Rebuild(data);
```

so the method tail reads:

```csharp
        var primaryDuration = TimeSpan.FromHours(Math.Max(1, _options.CurrentValue.RefreshIntervalInHours));
        _cache.Set(PrimaryCacheKey, data, primaryDuration);
        _cache.Set(StaleCacheKey, data, new MemoryCacheEntryOptions { SlidingExpiration = StaleFallbackDuration });

        await WriteCacheFileAsync(data, cancellationToken);
        _indexer.Rebuild(data);
        _logger.LogInformation("Refreshed {Count} community blog posts.", data.Posts.Count);
```

- [ ] **Step 4: Register the index and the indexer in `RegisterFeeds.cs`**

Add `using Examine;` at the top. In `Compose`, in the community blogs section, register the index and indexer (before `AddSingleton<ICommunityBlogsService, ...>`):

```csharp
        builder.Services.AddExamineLuceneIndex(CommunityBlogsSearchIndexer.IndexName);
        builder.Services.AddSingleton<ICommunityBlogsIndexer, CommunityBlogsSearchIndexer>();
```

(`AddExamineLuceneIndex` comes from Examine's DI extensions, available via the Examine package Umbraco already references. If the symbol is not found, add `using Examine.Lucene.DependencyInjection;`.)

- [ ] **Step 5: Update existing test construction if needed**

If Step 1's helper change requires it, update `CommunityBlogsTestHelpers.CreateService` to accept and pass the indexer. Ensure all existing `CommunityBlogsServiceTests` still compile.

- [ ] **Step 6: Run the full test class — verify pass**

Run: `dotnet test src/UmbracoCommunity.Web.Tests --filter CommunityBlogs`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add src/UmbracoCommunity.Web/Features/Feeds/Configuration/RegisterFeeds.cs \
        src/UmbracoCommunity.Web/Features/Feeds/CommunityBlogs/CommunityBlogsService.cs \
        src/UmbracoCommunity.Web.Tests/CommunityBlogs/
git commit -m "feat(community-blogs): register CommunityBlogsIndex and rebuild it on refresh"
```

---

## Task 6: Merge community posts into search results

`SearchService` queries `CommunityBlogsIndex` and merges hits with content hits by score. `SearchResultItem` gains `IsExternal`.

**Files:**
- Modify: `src/UmbracoCommunity.Web/Models/Pages/SearchPageViewModel.cs`
- Modify: `src/UmbracoCommunity.Web/Services/SearchService.cs`

- [ ] **Step 1: Add `IsExternal` to `SearchResultItem`**

In `SearchPageViewModel.cs`, add to `SearchResultItem`:

```csharp
    public bool IsExternal { get; init; }
```

- [ ] **Step 2: Build the community query + merge in `SearchService.SearchAsync`**

In `SearchService.cs`:

a) Add fields near the other static members:

```csharp
    private static readonly string CommunityIndexName = "CommunityBlogsIndex";
    private static readonly string[] CommunitySearchFields = { "title", "excerpt", "author" };
```

b) The current method builds `filtered` (content `ISearchResult`s) then maps `filtered.Skip(skip).Take(take)` into `items`. Refactor so both sources become a single scored list before paging. After building the content `items`-equivalent, instead of paginating content alone, build a combined list of `(double Score, SearchResultItem Item)`:

- Map each content result (the existing per-result mapping) into a `SearchResultItem` with `IsExternal = false`, paired with `result.Score`.
- Query the community index (guarded; missing index → empty):

```csharp
        var communityScored = new List<(float Score, SearchResultItem Item)>();
        if (_examineManager.TryGetIndex(CommunityIndexName, out var communityIndex))
        {
            try
            {
                var communityResults = communityIndex.Searcher
                    .CreateQuery()
                    .ManagedQuery(query, CommunitySearchFields)
                    .Execute(QueryOptions.SkipTake(0, MaxIndexFetch));

                foreach (var r in communityResults)
                {
                    var url = r.GetValues("url").FirstOrDefault();
                    var title = r.GetValues("title").FirstOrDefault();
                    if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(title)) continue;

                    communityScored.Add((r.Score, new SearchResultItem
                    {
                        Name = title,
                        Url = url,
                        Description = BuildExcerpt(r.GetValues("excerpt").FirstOrDefault()),
                        ContentTypeAlias = CommunityBlogsSearchIndexer.Category,
                        IsExternal = true,
                    }));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Search: community blogs query failed for '{Query}'", query);
            }
        }
```

c) Combine content-scored + community-scored, order by score descending, set `total = combined.Count`, and page with `Skip(skip).Take(take)`:

```csharp
        var combined = contentScored
            .Concat(communityScored.Select(c => ((double)c.Score, c.Item)))
            .OrderByDescending(x => x.Item1)
            .ToList();

        var total = combined.Count;
        var items = combined.Skip(skip).Take(take).Select(x => x.Item).ToList();

        return Task.FromResult<(IReadOnlyList<SearchResultItem>, int)>((items, total));
```

where `contentScored` is `List<(double, SearchResultItem)>` built from the existing `filtered` results (reuse the existing content→`SearchResultItem` mapping, pairing each with `result.Score`). Remove the old `total = filtered.Count` / single-source `items` block so there is one return path.

> Add `using UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;` for `CommunityBlogsSearchIndexer.Category`, or inline the literal `"communityBlogPost"`.

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/UmbracoCommunity.Web/UmbracoCommunity.Web.csproj`
Expected: Build succeeded.

- [ ] **Step 4: Manual verification**

With the app running and the feed refreshed at least once (background service runs on startup), search for a term you know appears in a community post title. Expect the post to appear among results. (Visual external marker added in Task 7.)

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.Web/Models/Pages/SearchPageViewModel.cs \
        src/UmbracoCommunity.Web/Services/SearchService.cs
git commit -m "feat(search): merge community blog posts into search results"
```

---

## Task 7: External-link indicator in search results

**Files:**
- Modify: `src/UmbracoCommunity.Web.UI/Views/SearchPage.cshtml`
- Modify: the search page stylesheet (find it: `grep -rl "search-page__result" src/UmbracoCommunity.StaticAssets/src/css`)

- [ ] **Step 1: Render external results with new-tab link + indicator**

In `SearchPage.cshtml`, replace the `<li class="search-page__result">...</li>` body with:

```cshtml
                    <li class="search-page__result @(item.IsExternal ? "is-external" : "")">
                        <h2 class="search-page__result-title">
                            @item.Name
                            @if (item.IsExternal)
                            {
                                <span class="search-page__result-external" aria-label="Opens an external site">External link ↗</span>
                            }
                        </h2>
                        @if (!string.IsNullOrWhiteSpace(item.Description))
                        {
                            <p class="search-page__result-description">@item.Description</p>
                        }
                        @if (item.IsExternal)
                        {
                            <a class="search-page__result-url" href="@item.Url" target="_blank" rel="noopener noreferrer">@item.Url</a>
                        }
                        else
                        {
                            <a class="search-page__result-url" href="@item.Url">@item.Url</a>
                        }
                    </li>
```

- [ ] **Step 2: Add the indicator style**

Append to the search page stylesheet (the file found above):

```css
.search-page__result-external {
  display: inline-block;
  margin-left: 0.5rem;
  padding: 0.1rem 0.5rem;
  border-radius: 999px;
  font-size: 0.75rem;
  font-weight: 600;
  vertical-align: middle;
  background: var(--color-light);
  color: var(--color-blue);
}
```

- [ ] **Step 3: Build frontend assets**

Run: `cd src/UmbracoCommunity.StaticAssets && npm run build`
Expected: completes without error.

- [ ] **Step 4: Manual verification**

Search for a community-post term → the result shows the "External link ↗" pill and opens in a new tab.

- [ ] **Step 5: Commit**

```bash
git add src/UmbracoCommunity.Web.UI/Views/SearchPage.cshtml src/UmbracoCommunity.StaticAssets/src/css
git commit -m "feat(search): mark community blog posts as external links in results"
```

---

## Task 8: Full verification + PR

- [ ] **Step 1: Run the whole test suite**

Run: `dotnet test src/UmbracoCommunity.Web.Tests`
Expected: all PASS.

- [ ] **Step 2: Full solution build + frontend build**

Run: `dotnet build` and `cd src/UmbracoCommunity.StaticAssets && npm run build`
Expected: both succeed.

- [ ] **Step 3: End-to-end manual check**

- Featured block: 6 cards, no pager, working Read more link (Image #12 parity).
- Full listing: 3 columns, no sidebar, blog/search-style pager (Image #13 parity).
- Search: community posts appear, marked external, open in new tab.

- [ ] **Step 4: Push and open PR against `develop`**

```bash
git push -u origin feat/community-blogs-refinement
gh pr create --base develop --title "Refine community blog posts: featured mode, full listing pager, search ingestion" --body "<summary + screenshots>"
```

---

## Self-review notes

- **Spec coverage:** Featured toggle+count (Task 1, 2) ✓; Read more link (Task 1, 2) ✓; full listing 3-col no sidebar (already 3-col at `--lg`; full mode is the block default) ✓; blog-style pager (Task 3) ✓; Examine ingestion on refresh (Task 4, 5) ✓; external marker in search (Task 6, 7) ✓; tests (Task 4 indexer, Task 5 service) ✓.
- **Type consistency:** `CommunityBlogsSearchIndexer.IndexName`/`.Category`/`.BuildValueSets`/`Rebuild` referenced consistently across Tasks 4–6. `SearchResultItem.IsExternal` added in Task 6 Step 1, used in Tasks 6–7. `ReadMoreLink` is a single `Umbraco.Cms.Core.Models.Link` (verified against sibling generated models).
- **Open risk:** `AddExamineLuceneIndex` namespace and the exact `CommunityBlogsTestHelpers.CreateService` signature are the two spots most likely to need a small local adjustment during execution — both are flagged inline.

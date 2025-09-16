# Building Pages

This guide provides step-by-step instructions for creating new pages in this solution, following the established patterns and best practices.

## Overview

The solution uses a structured approach for building pages with the following components:

1. **Document Type** - Umbraco content structure
2. **Controller** - Handles HTTP requests and route hijacking
3. **View Model** - Strongly-typed data model for the view
4. **View Model Builder** - Converts Umbraco content to view models
5. **View** - Razor template for rendering
6. **Tests** - Unit tests for the view model builder

## Prerequisites

Before creating a new page, ensure you have:

- Access to the Umbraco backoffice
- Understanding of the existing patterns (see examples below)
- Development environment set up (see main README)

## Step-by-Step Guide

### Step 1: Create the Document Type

**In Umbraco Backoffice:**
- Go to **Settings** → **Document Types**
- Create a new document type or use an existing one
- Define the properties your page needs
- Set up compositions 
- Assign a template

### Step 2: Generate Models Builder Classes

After creating the document type, Models Builder will **not** automatically generate strongly-typed classes. You need to do it manually.

### Step 3: Create the View Model

Create a view model that inherits from `PageViewModelBase`:

```csharp
// src/UmbracoDotCom.Web/Models/ViewModels/Pages/MyCustomPageViewModel.cs
namespace UmbracoDotCom.Web.Models.ViewModels.Pages;

public class MyCustomPageViewModel : PageViewModelBase
{
    public MyCustomPageViewModel(IPublishedContent currentPage)
        : base(currentPage)
    {
    }

    public string PageTitle { get; init; } = string.Empty;
    public string PageContent { get; init; } = string.Empty;
    public string? HeroImageUrl { get; init; }
    public IReadOnlyList<BlockViewModelBase> BlockContent { get; init; } = new List<BlockViewModelBase>().AsReadOnly();
}
```

### Step 4: Create the View Model Builder

Create a builder that implements `IViewModelBuilder<T>` or `IAsyncViewModelBuilder<T>`:

```csharp
// src/UmbracoDotCom.Web/ViewModelBuilders/Pages/MyCustomPageViewModelBuilder.cs
namespace UmbracoDotCom.Web.ViewModelBuilders.Pages;

internal class MyCustomPageViewModelBuilder : ViewModelBuilderBase, IAsyncViewModelBuilder<MyCustomPageViewModel>
{
    private readonly IBlockCollectionBuilder _blockCollectionBuilder;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;
    private readonly IPublishedUrlProvider _publishedUrlProvider;

    public MyCustomPageViewModelBuilder(
        IPublishedUrlProvider publishedUrlProvider,
        IBlockCollectionBuilder blockCollectionBuilder,
        IImageUrlGenerator imageUrlGenerator,
        IPublishedValueFallback publishedValueFallback)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _blockCollectionBuilder = blockCollectionBuilder;
        _imageUrlGenerator = imageUrlGenerator;
        _publishedValueFallback = publishedValueFallback;
    }

    public async Task<MyCustomPageViewModel> BuildAsync(IPublishedContent currentPage, IUmbracoContext umbracoContext)
    {
        MyCustomPage contentModel = currentPage.As<MyCustomPage>();

        var viewModel = new MyCustomPageViewModel(currentPage)
        {
            PageTitle = contentModel.PageTitle ?? string.Empty,
            PageContent = contentModel.PageContent?.ToString() ?? string.Empty,
            HeroImageUrl = GetImageUrl(
                contentModel.HeroImage,
                Constants.Presentation.Crops.Hero,
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider),
            BlockContent = await _blockCollectionBuilder.BuildAsync(contentModel.BlockContent ?? BlockListModel.Empty)
        };

        return viewModel;
    }
}
```

### Step 5: Create the Controller

Create a controller that inherits from `RenderController`:

```csharp
// src/UmbracoDotCom.Web/Controllers/MyCustomPageController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Web.Common.Controllers;
using UmbracoDotCom.Web.Attributes;
using UmbracoDotCom.Web.ViewModelBuilders;

namespace UmbracoDotCom.Web.Controllers;

public class MyCustomPageController : RenderController
{
    private readonly IAsyncViewModelBuilder<MyCustomPageViewModel> _viewModelBuilder;

    public MyCustomPageController(
        ILogger<MyCustomPageController> logger,
        ICompositeViewEngine compositeViewEngine,
        IUmbracoContextAccessor umbracoContextAccessor,
        IAsyncViewModelBuilder<MyCustomPageViewModel> viewModelBuilder)
        : base(logger, compositeViewEngine, umbracoContextAccessor) => _viewModelBuilder = viewModelBuilder;

    [NonAction]
    public sealed override IActionResult Index() => throw new NotImplementedException();

    [ApplyCommonElements]
    [ApplyPageMetaData]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        MyCustomPageViewModel viewModel = await _viewModelBuilder.BuildAsync(
            CurrentPage ?? throw new InvalidOperationException($"Cannot build view model as {nameof(CurrentPage)} is null."),
            UmbracoContext);
        return CurrentTemplate(viewModel);
    }
}
```

### Step 6: Register Dependencies

Register your view model builder in the DI container:

```csharp
// src/UmbracoDotCom.Web/Extensions/UmbracoBuilderExtensions.cs
public static IUmbracoBuilder AddViewModelBuildersAndDecorators(this IUmbracoBuilder builder)
{
    // ... existing registrations ...
    builder.Services.AddScoped<IAsyncViewModelBuilder<MyCustomPageViewModel>, MyCustomPageViewModelBuilder>();
    
    return builder;
}
```

### Step 7: Create the View

Create a Razor view for your page:

```cshtml
@* src/UmbracoDotCom.Web.UI/Views/MyCustomPage.cshtml *@
@model UmbracoDotCom.Web.Models.ViewModels.Pages.MyCustomPageViewModel
@{
    Layout = "_Layout";
}

(...)
```

### Step 8: Create Unit Tests

Create comprehensive unit tests for your view model builder:

```csharp
// tests/UmbracoDotCom.Web.Tests/ViewModelBuilders/Pages/MyCustomPageViewModelBuilderTests.cs
namespace UmbracoDotCom.Web.Tests.ViewModelBuilders.Pages;

public class MyCustomPageViewModelBuilderTests : ViewModelBuilderTestsBase
{
    private readonly MyCustomPageViewModelBuilder _builder;

    public MyCustomPageViewModelBuilderTests()
    {
        _builder = new MyCustomPageViewModelBuilder(
            MockPublishedUrlProvider.Object,
            MockBlockCollectionBuilder.Object,
            MockImageUrlGenerator.Object,
            MockPublishedValueFallback.Object);
    }

    [Fact]
    public async Task BuildAsync_WithValidContent_ReturnsCorrectViewModel()
    {
        // Arrange
        var content = CreateTypedContent<MyCustomPage>();
        SetPropertyValue(content, "pageTitle", "Test Page Title");
        SetPropertyValue(content, "pageContent", "<p>Test content</p>");

        // Act
        var result = await _builder.BuildAsync(content, MockUmbracoContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Test Page Title", result.PageTitle);
        Assert.Equal("<p>Test content</p>", result.PageContent);
    }

    [Fact]
    public async Task BuildAsync_WithNullContent_ReturnsEmptyStrings()
    {
        // Arrange
        var content = CreateTypedContent<MyCustomPage>();

        // Act
        var result = await _builder.BuildAsync(content, MockUmbracoContext.Object);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(string.Empty, result.PageTitle);
        Assert.Equal(string.Empty, result.PageContent);
    }
}
```

## Advanced Patterns

### Pages with Input Models

For pages that accept query parameters or form data:

```csharp
// Input Model
public class MyCustomPageInputModel
{
    public int PageNumber { get; set; } = 1;
    public string? Filter { get; set; }
}

// View Model Builder
public class MyCustomPageViewModelBuilder : ViewModelBuilderBase, IViewModelBuilder<MyCustomPageInputModel, MyCustomPageViewModel>
{
    public MyCustomPageViewModel Build(IPublishedContent currentPage, IUmbracoContext umbracoContext, MyCustomPageInputModel inputModel)
    {
        // Use inputModel.PageNumber and inputModel.Filter
        // ...
    }
}

// Controller
public IActionResult Index(int page = 1, string? filter = null)
{
    var inputModel = new MyCustomPageInputModel
    {
        PageNumber = page,
        Filter = filter
    };
    
    var viewModel = _viewModelBuilder.Build(CurrentPage, UmbracoContext, inputModel);
    return CurrentTemplate(viewModel);
}
```

### Pages with Block Content

For pages that use Umbraco's Block List editor:

```csharp
// In your view model builder
public async Task<MyCustomPageViewModel> BuildAsync(IPublishedContent currentPage, IUmbracoContext umbracoContext)
{
    MyCustomPage contentModel = currentPage.As<MyCustomPage>();

    var viewModel = new MyCustomPageViewModel(currentPage)
    {
        BlockContent = await _blockCollectionBuilder.BuildAsync(contentModel.BlockContent ?? BlockListModel.Empty)
    };

    return viewModel;
}

// In your view
@foreach (var block in Model.BlockContent)
{
    @await Html.PartialAsync($"Blocks/{block.GetType().Name.Replace("ViewModel", "")}", block)
}
```

### Pages with Schema Markup

For pages that need structured data:

```csharp
// In your view model builder
public async Task<MyCustomPageViewModel> BuildAsync(IPublishedContent currentPage, IUmbracoContext umbracoContext)
{
    var viewModel = new MyCustomPageViewModel(currentPage)
    {
        // ... other properties
    };

    // Add schema markup
    var schema = new WebPage
    {
        Name = viewModel.PageTitle,
        Description = viewModel.MetaDescription,
        Url = new Uri("https://umbraco.com" + currentPage.Url())
    };

    viewModel.AddSchemaMarkup(schema.ToHtmlEscapedString());

    return viewModel;
}
```

## Common Attributes

### Controller Attributes

- `[ApplyCommonElements]` - Applies header, footer, and other common elements
- `[ApplyPageMetaData]` - Applies SEO metadata
- `[ApplyCertifiedDeveloperPageMetaData]` - Special metadata for developer pages

### View Model Builder Patterns

- Use `currentPage.As<T>()` for safe casting to typed models
- Use `GetImageUrl()` helper for media handling
- Use `_blockCollectionBuilder.BuildAsync()` for block content
- Add schema markup using `viewModel.AddSchemaMarkup()`

## Testing Best Practices

1. **Test all properties** - Ensure all view model properties are correctly mapped
2. **Test null handling** - Verify behavior when content is null or empty
3. **Test async operations** - Use `await` for async builders
4. **Mock dependencies** - Use the base test class helpers
5. **Test edge cases** - Handle missing properties, invalid data, etc.

## Examples

See these existing implementations for reference:

- **HomePage**: `HomePageController`, `HomePageViewModel`, `HomePageViewModelBuilder`
- **BlogPost**: `BlogPostController`, `BlogPostPageViewModel`, `BlogPostPageViewModelBuilder`
- **Search**: `SearchController`, `SearchPageViewModel`, `SearchPageViewModelBuilder`
- **ContentPage**: `ContentPageController`, `ContentPageViewModel`, `ContentPageViewModelBuilder`

## Troubleshooting

### Common Issues

1. **Models Builder not generating** - Check document type configuration and rebuild
2. **Controller not found** - Ensure controller name matches document type alias
3. **View not found** - Check view location and naming conventions
4. **Dependency injection errors** - Verify all services are registered
5. **Block content not rendering** - Check block view model builders are registered

### Debugging Tips

1. Use `[NonAction]` attribute to prevent conflicts with base controller methods
2. Check Umbraco logs for detailed error information
3. Use breakpoints in view model builders to inspect data flow
4. Verify Models Builder classes are up to date

## Next Steps

After creating your page:

1. **Add content** in the Umbraco backoffice
2. **Test the page** in your local environment
3. **Add styling** in the StaticAssets project
4. **Update navigation** if needed
5. **Add to search** if the page should be searchable
6. **Configure SEO** settings in the backoffice

For more information about specific patterns or components, refer to the existing codebase examples and the main README file. 
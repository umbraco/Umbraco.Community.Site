# Building Blocks

This guide provides step-by-step instructions for creating new content blocks in this solution, following the established patterns and best practices.

## Overview

Content blocks are reusable components that can be added to pages using Umbraco's Block List editor. Each block consists of:

1. **Element Type** - Umbraco content structure for the block
2. **Settings Type** - Optional configuration for the block
3. **View Model** - Strongly-typed data model for the view
4. **View Model Builder** - Converts Umbraco content to view models
5. **View** - Razor template for rendering
6. **Tests** - Unit tests for the view model builder

## Prerequisites

Before creating a new block, ensure you have:

- Access to the Umbraco backoffice
- Understanding of the existing patterns (see examples below)
- Development environment set up (see main README)

## Step-by-Step Guide

### Step 1: Create the Element Type

**In Umbraco Backoffice:**
   - Go to **Settings** → **Document Types**
   - Create a new element type (not document type)
   - Define the properties your block needs
   - Set up compositions if needed

### Step 2: Create the Settings Type (Optional)

For blocks that need configuration options:

1. **Create Settings Element Type:**
   ```
   MyCustomBlockSettings
   ├── Properties
   │   ├── Background Color (Color Picker)
   │   ├── Text Alignment (Radio Button List)
   │   └── Max Width (Number)
   ```

2. **Settings are optional** - Use `EmptyBlockSettings` if no settings are needed

### Step 3: Generate Models Builder Classes

After creating the element types, Models Builder will **not** automatically generate strongly-typed classes. You have to do it manually.

### Step 4: Create the View Model

Create a view model that inherits from `BlockViewModelBase`:

```csharp
// src/UmbracoCommunity.Web/Models/ViewModels/Blocks/MyCustomBlockViewModel.cs
namespace UmbracoCommunity.Web.Models.ViewModels.Blocks;

public class MyCustomBlockViewModel : BlockViewModelBase
{
    public required string Headline { get; init; }
    public required string Text { get; init; }
    public ImageViewModel? Image { get; init; }
    public LinkViewModel? Link { get; init; }
    public string BackgroundColor { get; init; } = string.Empty;
    public string TextColor { get; init; } = string.Empty;
    public string Alignment { get; init; } = "left";

    public bool HasImage => Image is not null;
    public bool HasLink => Link is not null && !string.IsNullOrEmpty(Link.Url);

    public override int GetWordCount() => Text.GetWordCount();
}
```

### Step 5: Create the View Model Builder

Create a builder that inherits from `BlockViewModelBuilderBase<TContentModel, TSettingsModel>`:

```csharp
// src/UmbracoCommunity.Web/ViewModelBuilders/Blocks/MyCustomBlockViewModelBuilder.cs
namespace UmbracoCommunity.Web.ViewModelBuilders.Blocks;

internal class MyCustomBlockViewModelBuilder : BlockViewModelBuilderBase<MyCustomBlock, MyCustomBlockSettings>
{
    private readonly IPublishedUrlProvider _publishedUrlProvider;
    private readonly IImageUrlGenerator _imageUrlGenerator;
    private readonly IPublishedValueFallback _publishedValueFallback;

    public MyCustomBlockViewModelBuilder(
        IPublishedUrlProvider publishedUrlProvider,
        IImageUrlGenerator imageUrlGenerator,
        IPublishedValueFallback publishedValueFallback)
    {
        _publishedUrlProvider = publishedUrlProvider;
        _imageUrlGenerator = imageUrlGenerator;
        _publishedValueFallback = publishedValueFallback;
    }

    public override BlockViewModelBase Build(MyCustomBlock blockModel, MyCustomBlockSettings blockSettingsModel)
    {
        return new MyCustomBlockViewModel
        {
            Headline = blockModel.Headline ?? string.Empty,
            Text = blockModel.Text?.ToString() ?? string.Empty,
            Image = GetImageViewModel(
                blockModel.Image,
                Constants.Presentation.Crops.Hero,
                blockModel.ImageAltText,
                _imageUrlGenerator,
                _publishedValueFallback,
                _publishedUrlProvider),
            Link = blockModel.Link is not null ? new LinkViewModel(blockModel.Link) : null,
            BackgroundColor = GetRequiredColor(blockSettingsModel.Colors?.BackgroundColor, Constants.Presentation.Colors.Transparent),
            TextColor = GetRequiredColor(blockSettingsModel.Colors?.ForegroundColor, Constants.Presentation.Colors.Darkest),
            Alignment = blockSettingsModel.TextAlignment?.ToLowerInvariant() ?? "left"
        };
    }
}
```

### Step 6: Register Dependencies

Register your block view model builder in the DI container:

```csharp
// src/UmbracoCommunity.Web/Extensions/UmbracoBuilderExtensions.cs
public static IUmbracoBuilder AddViewModelBuildersAndDecorators(this IUmbracoBuilder builder)
{
    // ... existing registrations ...
    builder.Services.AddScoped<IBlockViewModelBuilder, MyCustomBlockViewModelBuilder>();
    
    return builder;
}
```

### Step 7: Create the View

Create a Razor partial view for your block:

```cshtml
@* src/UmbracoCommunity.Web.UI/Views/Partials/Blocks/MyCustomBlock.cshtml *@
@model MyCustomBlockViewModel

(...)
```

### Step 8: Add Styling

Create CSS for your block:

```css
/* src/UmbracoCommunity.StaticAssets/src/css/blocks/my-custom-block.css */
.dc-my-custom-block {
    padding: var(--unit-lg);
    background-color: var(--block-background-color, transparent);
    color: var(--block-text-color, var(--color-darkest));
}

/* Responsive adjustments */
@media (max-width: 768px) {
    .dc-my-custom-block {
        padding: var(--unit-md);
    }
}
```

Import your CSS in the main blocks file:

```css
/* src/UmbracoCommunity.StaticAssets/src/css/blocks/blocks.css */
@import './my-custom-block.css';
```

### Step 9: Create Unit Tests

Create comprehensive unit tests for your block view model builder:

```csharp
// tests/UmbracoCommunity.Web.Tests/ViewModelBuilders/Blocks/MyCustomBlockViewModelBuilderTests.cs
namespace UmbracoCommunity.Web.Tests.ViewModelBuilders.Blocks;

public class MyCustomBlockViewModelBuilderTests : ViewModelBuilderTestsBase
{
    private readonly MyCustomBlockViewModelBuilder _builder;

    public MyCustomBlockViewModelBuilderTests()
    {
        _builder = new MyCustomBlockViewModelBuilder(
            MockPublishedUrlProvider.Object,
            MockImageUrlGenerator.Object,
            MockPublishedValueFallback.Object);
    }

    [Fact]
    public void Build_WithValidContent_ReturnsCorrectViewModel()
    {
        // Arrange
        var content = CreateTypedElement<MyCustomBlock>();
        var settings = CreateTypedElement<MyCustomBlockSettings>();
        
        SetPropertyValue(content, "headline", "Test Headline");
        SetPropertyValue(content, "text", "<p>Test content</p>");
        SetPropertyValue(settings, "textAlignment", "Center");

        // Act
        var result = _builder.Build(content, settings);

        // Assert
        Assert.NotNull(result);
        Assert.IsType<MyCustomBlockViewModel>(result);
        
        var viewModel = (MyCustomBlockViewModel)result;
        Assert.Equal("Test Headline", viewModel.Headline);
        Assert.Equal("<p>Test content</p>", viewModel.Text);
        Assert.Equal("center", viewModel.Alignment);
    }

    [Fact]
    public void Build_WithNullContent_ReturnsEmptyStrings()
    {
        // Arrange
        var content = CreateTypedElement<MyCustomBlock>();
        var settings = CreateTypedElement<MyCustomBlockSettings>();

        // Act
        var result = _builder.Build(content, settings);

        // Assert
        Assert.NotNull(result);
        var viewModel = (MyCustomBlockViewModel)result;
        Assert.Equal(string.Empty, viewModel.Headline);
        Assert.Equal(string.Empty, viewModel.Text);
        Assert.Equal("left", viewModel.Alignment);
    }

    [Fact]
    public void Build_WithImage_ReturnsImageViewModel()
    {
        // Arrange
        var content = CreateTypedElement<MyCustomBlock>();
        var settings = CreateTypedElement<MyCustomBlockSettings>();
        var mediaWithCrops = CreateMediaWithCrops("test-image.jpg", "Test Image");
        
        SetPropertyValue(content, "image", mediaWithCrops);

        // Act
        var result = _builder.Build(content, settings);

        // Assert
        var viewModel = (MyCustomBlockViewModel)result;
        Assert.NotNull(viewModel.Image);
        Assert.True(viewModel.HasImage);
    }
}
```

## Advanced Patterns

### Blocks with Nested Block Lists

For blocks that contain other blocks:

```csharp
// View Model
public class MyContainerBlockViewModel : BlockViewModelBase
{
    public required string Headline { get; init; }
    public IReadOnlyList<BlockViewModelBase> Items { get; init; } = new List<BlockViewModelBase>().AsReadOnly();
}

// View Model Builder
internal class MyContainerBlockViewModelBuilder : BlockViewModelBuilderBase<MyContainerBlock, MyContainerBlockSettings>
{
    private readonly IBlockCollectionBuilder _blockCollectionBuilder;

    public MyContainerBlockViewModelBuilder(IBlockCollectionBuilder blockCollectionBuilder)
    {
        _blockCollectionBuilder = blockCollectionBuilder;
    }

    public override BlockViewModelBase Build(MyContainerBlock blockModel, MyContainerBlockSettings blockSettingsModel)
    {
        return new MyContainerBlockViewModel
        {
            Headline = blockModel.Headline ?? string.Empty,
            Items = _blockCollectionBuilder.Build(blockModel.Items ?? BlockListModel.Empty)
        };
    }
}

// View
@model MyContainerBlockViewModel

<div class="dc-my-container-block" id="@Model.IdHash">
    <h2>@Model.Headline</h2>
    <div class="dc-my-container-block__items">
        @foreach (var item in Model.Items)
        {
            @await Html.PartialAsync($"Blocks/{item.GetType().Name.Replace("ViewModel", "")}", item)
        }
    </div>
</div>
```

### Blocks with Async Operations

For blocks that need async operations (e.g., API calls):

```csharp
// Async Block View Model Builder
internal class MyAsyncBlockViewModelBuilder : BlockViewModelBuilderBase<MyAsyncBlock, EmptyBlockSettings>, IAsyncBlockViewModelBuilder
{
    private readonly IMyService _myService;

    public MyAsyncBlockViewModelBuilder(IMyService myService)
    {
        _myService = myService;
    }

    public override BlockViewModelBase Build(MyAsyncBlock blockModel, EmptyBlockSettings blockSettingsModel)
    {
        // Synchronous fallback
        return new MyAsyncBlockViewModel
        {
            Headline = blockModel.Headline ?? string.Empty,
            Items = new List<string>().AsReadOnly()
        };
    }

    public async Task<BlockViewModelBase> BuildAsync(MyAsyncBlock blockModel, EmptyBlockSettings blockSettingsModel)
    {
        var items = await _myService.GetItemsAsync(blockModel.Category);
        
        return new MyAsyncBlockViewModel
        {
            Headline = blockModel.Headline ?? string.Empty,
            Items = items.AsReadOnly()
        };
    }
}
```

### Blocks with Complex Image Handling

For blocks with multiple images or responsive images:

```csharp
// View Model Builder
public override BlockViewModelBase Build(MyImageBlock blockModel, MyImageBlockSettings blockSettingsModel)
{
    return new MyImageBlockViewModel
    {
        DesktopImage = GetImageViewModel(
            blockModel.Image,
            "desktop",
            blockModel.ImageAltText,
            _imageUrlGenerator,
            _publishedValueFallback,
            _publishedUrlProvider),
        MobileImage = GetImageViewModel(
            blockModel.Image,
            "mobile",
            blockModel.ImageAltText,
            _imageUrlGenerator,
            _publishedValueFallback,
            _publishedUrlProvider),
        ThumbnailImage = GetImageViewModel(
            blockModel.Image,
            "thumbnail",
            blockModel.ImageAltText,
            _imageUrlGenerator,
            _publishedValueFallback,
            _publishedUrlProvider)
    };
}

// View
@if (Model.HasImage)
{
    <picture>
        <source media="(min-width: 768px)" srcset="@Model.DesktopImage!.Url">
        <source media="(max-width: 767px)" srcset="@Model.MobileImage!.Url">
        <img src="@Model.DesktopImage!.Url" alt="@Model.DesktopImage.AltText" loading="lazy">
    </picture>
}
```

### Blocks with Color Themes

For blocks with configurable colors:

```csharp
// View Model Builder
public override BlockViewModelBase Build(MyColorBlock blockModel, MyColorBlockSettings blockSettingsModel)
{
    var backgroundColor = GetRequiredColor(blockSettingsModel.Colors?.BackgroundColor, Constants.Presentation.Colors.White);
    var textColor = GetTextColorForBackground(backgroundColor);

    return new MyColorBlockViewModel
    {
        BackgroundColor = backgroundColor,
        TextColor = textColor,
        Headline = blockModel.Headline ?? string.Empty
    };
}

private static string GetTextColorForBackground(string backgroundColor)
{
    return backgroundColor.StartsWith("#fff") || backgroundColor == Constants.Presentation.Colors.White
        ? Constants.Presentation.Colors.Darkest
        : "#ffffff";
}

// View
<div class="dc-my-color-block" id="@Model.IdHash">
    <style asp-add-nonce="true">
        #@Model.IdHash {
            --block-background-color: @Model.BackgroundColor;
            --block-text-color: @Model.TextColor;
        }
    </style>
    <div class="dc-my-color-block__content">
        <h2>@Model.Headline</h2>
    </div>
</div>
```

## Common Helper Methods

### Image Handling

```csharp
// Get image with specific crop
var image = GetImageViewModel(
    blockModel.Image,
    "hero", // crop alias
    blockModel.ImageAltText,
    _imageUrlGenerator,
    _publishedValueFallback,
    _publishedUrlProvider);

// Get image with custom dimensions
var imageUrl = GetImageUrl(
    blockModel.Image,
    string.Empty,
    _imageUrlGenerator,
    _publishedValueFallback,
    _publishedUrlProvider,
    width: 800,
    height: 600);
```

### Color Handling

```csharp
// Get required color with default
var backgroundColor = GetRequiredColor(
    blockSettingsModel.Colors?.BackgroundColor, 
    Constants.Presentation.Colors.White);

// Get optional color (can be null)
var textColor = GetOptionalColor(blockSettingsModel.Colors?.TextColor);
```

### Link Handling

```csharp
// Create link view model
var link = blockModel.Link is not null 
    ? new LinkViewModel(blockModel.Link) 
    : null;

// Check if link exists
var hasLink = link is not null && !string.IsNullOrEmpty(link.Url);
```

## Testing Best Practices

1. **Test all properties** - Ensure all view model properties are correctly mapped
2. **Test null handling** - Verify behavior when content is null or empty
3. **Test settings** - Test both with and without settings
4. **Test image handling** - Mock image properties and verify URLs
5. **Test color logic** - Test color fallbacks and calculations
6. **Test computed properties** - Test properties like `HasImage`, `HasLink`

## Examples

See these existing implementations for reference:

- **TextBlock**: `TextBlockViewModel`, `TextBlockViewModelBuilder`, `TextBlock.cshtml`
- **ImageAndTextBlock**: `ImageAndTextBlockViewModel`, `ImageAndTextBlockViewModelBuilder`, `ImageAndTextBlock.cshtml`
- **CallToActionBlock**: `CallToActionBlockViewModel`, `CallToActionBlockViewModelBuilder`, `CallToActionBlock.cshtml`
- **QuoteBlock**: `QuoteBlockViewModel`, `QuoteBlockViewModelBuilder`, `QuoteBlock.cshtml`
- **ResultsBlock**: `ResultsBlockViewModel`, `ResultsBlockViewModelBuilder`, `ResultsBlock.cshtml`
- **SliderBlock**: `SliderBlock.cshtml` — Container block with nested `SlideItemBlockWithTag` / `SlideItemBlockWithIcon` child items, dark/light background theming, and `dc-slider` / `dc-slider-controls` web components

## Troubleshooting

### Common Issues

1. **Block not appearing in Block List** - Check element type configuration and registration
2. **View not found** - Ensure view name matches block alias
3. **Images not loading** - Check crop aliases and image URL generation
4. **Colors not applying** - Verify color picker configuration and CSS variables
5. **Nested blocks not rendering** - Check `IBlockCollectionBuilder` registration

### Debugging Tips

1. Check Umbraco logs for detailed error information
2. Use breakpoints in view model builders to inspect data flow
3. Verify Models Builder classes are up to date
4. Check CSS class names match between view and stylesheet
5. Test with different content scenarios

## Next Steps

After creating your block:

1. **Add to Block List** - Configure the block in Umbraco backoffice
2. **Test the block** - Add content and verify rendering
3. **Add styling** - Ensure responsive design and accessibility
4. **Update documentation** - Document any special usage requirements
5. **Add to pages** - Use the block in page content

For more information about specific patterns or components, refer to the existing codebase examples and the main README file. 
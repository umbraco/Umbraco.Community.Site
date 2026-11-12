using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Deploy;
using Umbraco.Cms.Core.Models;

namespace UmbracoCommunity.Web.Features.YouTube;

/// <summary>Makes Umbraco Deploy bring the poster image along when content with a YouTube video is transferred.</summary>
public sealed class YouTubeVideoValueConnector : IValueConnector
{
    public IEnumerable<string> PropertyEditorAliases => [YouTubeVideoPropertyEditor.EditorAlias];

    public Task<string?> ToArtifactAsync(
        object? value,
        IPropertyType propertyType,
        ICollection<ArtifactDependency> dependencies,
        IContextCache contextCache,
        CancellationToken cancellationToken = default)
    {
        var parsed = YouTubeVideoValue.TryParse(value);
        if (parsed?.PosterKey is { } posterKey)
        {
            dependencies.Add(new ArtifactDependency(Udi.Create(CoreConstants.UdiEntityType.Media, posterKey), false, ArtifactDependencyMode.Exist, null));
        }

        return Task.FromResult(parsed?.ToJson());
    }

    // Deploy keeps media keys across environments, so the stored value transfers unchanged.
    public Task<object?> FromArtifactAsync(
        string? value,
        IPropertyType propertyType,
        object? currentValue,
        IContextCache contextCache,
        CancellationToken cancellationToken = default)
        => Task.FromResult<object?>(value);
}

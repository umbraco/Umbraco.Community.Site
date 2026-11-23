using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Editors;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Strings;

namespace UmbracoCommunity.Web.Features.YouTube;

/// <summary>Schema for the YouTube video picker. Stores <see cref="YouTubeVideoValue"/> as JSON.</summary>
[DataEditor(EditorAlias, ValueType = ValueTypes.Json, ValueEditorIsReusable = true)]
public sealed class YouTubeVideoPropertyEditor : DataEditor
{
    public const string EditorAlias = "UmbracoCommunity.YouTubeVideo";

    public YouTubeVideoPropertyEditor(IDataValueEditorFactory dataValueEditorFactory)
        : base(dataValueEditorFactory)
        => SupportsReadOnly = true;

    protected override IDataValueEditor CreateValueEditor()
        => DataValueEditorFactory.Create<YouTubeVideoValueEditor>(Attribute!);
}

/// <summary>Reports the poster as a media reference, so "Used by" and delete warnings know about it.</summary>
public sealed class YouTubeVideoValueEditor : DataValueEditor, IDataValueReference
{
    public YouTubeVideoValueEditor(
        IShortStringHelper shortStringHelper,
        IJsonSerializer jsonSerializer,
        IIOHelper ioHelper,
        DataEditorAttribute attribute)
        : base(shortStringHelper, jsonSerializer, ioHelper, attribute)
    {
    }

    public IEnumerable<UmbracoEntityReference> GetReferences(object? value)
    {
        if (YouTubeVideoValue.TryParse(value)?.PosterKey is { } posterKey)
        {
            yield return new UmbracoEntityReference(
                Udi.Create(CoreConstants.UdiEntityType.Media, posterKey),
                CoreConstants.Conventions.RelationTypes.RelatedMediaAlias);
        }
    }
}

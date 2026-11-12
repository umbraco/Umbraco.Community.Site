using Umbraco.Cms.Core.Models.PublishedContent;

namespace UmbracoCommunity.Web.Models.ViewModels.Components;

public sealed record PageSuggestionsViewModel(string Heading, IReadOnlyList<IPublishedContent> Items);

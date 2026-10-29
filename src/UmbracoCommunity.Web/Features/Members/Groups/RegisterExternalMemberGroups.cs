using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace UmbracoCommunity.Web.Features.Members.Groups;

public sealed class RegisterExternalMemberGroups : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.Services.AddScoped<ExternalMemberGroupService>();
}

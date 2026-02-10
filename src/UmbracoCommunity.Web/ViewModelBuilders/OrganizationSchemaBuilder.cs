using Schema.NET;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.ViewModelBuilders;

/// <summary>
/// Builds Organization schema from site settings for multi-tenant support.
/// Falls back to Umbraco default organization data if not configured.
/// </summary>
internal class OrganizationSchemaBuilder
{
    // Default Umbraco organization data used when site settings are not configured
    private const string DefaultOrganizationName = "Umbraco";
    private const string DefaultOrganizationUrl = "https://umbraco.com";
    private const string DefaultOrganizationLogo = "https://umbraco.com/media/ntljbdhh/umbraco_logo_blue.svg";

    /// <summary>
    /// Builds an Organization schema from the site's social settings.
    /// Falls back to Umbraco defaults if organization data is not configured.
    /// </summary>
    public Organization Build(SocialSettings? socialSettings)
    {
        // Try to get organization data from site settings
        var organizationName = socialSettings?.OrganisationName;
        var organizationUrl = socialSettings?.OrganisationUrl;
        var organizationLogo = socialSettings?.OrganisationLogo;

        // Use site settings if configured, otherwise fall back to Umbraco defaults
        var hasCustomSettings = !string.IsNullOrEmpty(organizationName);

        var organization = new Organization
        {
            Name = hasCustomSettings ? organizationName : DefaultOrganizationName
        };

        // Add organization URL
        var urlToUse = hasCustomSettings && !string.IsNullOrEmpty(organizationUrl)
            ? organizationUrl
            : DefaultOrganizationUrl;

        Uri? baseUri = null;
        if (Uri.TryCreate(urlToUse, UriKind.Absolute, out var orgUri))
        {
            organization.Url = orgUri;
            baseUri = orgUri;
        }

        // Add logo
        if (hasCustomSettings && organizationLogo is not null)
        {
            var logoUrl = organizationLogo.MediaUrl();
            // If URL is relative, make it absolute using the organization URL as base
            if (!string.IsNullOrEmpty(logoUrl))
            {
                if (Uri.TryCreate(logoUrl, UriKind.Absolute, out var absoluteLogoUri))
                {
                    organization.Logo = absoluteLogoUri;
                }
                else if (baseUri is not null && Uri.TryCreate(baseUri, logoUrl, out var relativeLogoUri))
                {
                    organization.Logo = relativeLogoUri;
                }
            }
        }
        else
        {
            // Use Umbraco default logo
            if (Uri.TryCreate(DefaultOrganizationLogo, UriKind.Absolute, out var defaultLogoUri))
            {
                organization.Logo = defaultLogoUri;
            }
        }

        return organization;
    }
}

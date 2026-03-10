namespace UmbracoCommunity.BlockRestrictions;

/// <summary>
/// Shared constants used across the Block Restrictions package.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The Swagger/OpenAPI document name for this package's backoffice API.
    /// This name is used to group endpoints in the generated API docs and to
    /// associate the security filter that enables backoffice token authentication.
    /// Must match the value used in <see cref="BlockRestrictionComposer"/> and
    /// <see cref="Controllers.BlockRestrictionApiControllerBase"/>.
    /// </summary>
    public const string ApiName = "umbracocommunityblockrestrictions";
}

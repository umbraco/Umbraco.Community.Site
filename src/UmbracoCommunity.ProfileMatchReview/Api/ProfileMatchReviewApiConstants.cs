namespace UmbracoCommunity.ProfileMatchReview.Api;

/// <summary>Constants for the profile match review backoffice management API.</summary>
public static class ProfileMatchReviewApiConstants
{
    /// <summary>Swagger document / MapToApi group name. Also the route segment after <c>/umbraco/</c>.</summary>
    public const string ApiName = "profilematchreview";

    /// <summary>Alias of the backoffice user group whose members may review matches (administrators may too).</summary>
    public const string ReviewerGroupAlias = "profileMatchReviewers";
}

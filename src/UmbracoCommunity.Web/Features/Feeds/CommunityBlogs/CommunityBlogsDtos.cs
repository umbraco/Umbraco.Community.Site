using System.Text.Json;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>Response envelope from GET /v1/blog-posts.</summary>
public sealed record PostsResponseDto(
    IReadOnlyList<PublicPostDto> Data,
    PaginationDto Pagination);

public sealed record PublicPostDto(
    string Id,
    string Type,
    string? Platform,
    string? Title,
    string? Url,
    string? Content,
    string? CoverImageUrl,
    DateTimeOffset PublishedAt,
    PublicAuthorDto? Author);

public sealed record PublicAuthorDto(
    string? Name,
    string? ProfileUrl,
    string? AvatarUrl,
    string? Type);

public sealed record PaginationDto(
    string? NextCursor,
    bool HasMore);

/// <summary>Response envelope from GET /v1/profiles/lookup.</summary>
public sealed record ProfileLookupResponseDto(ProfileLookupDto Data);

public sealed record ProfileLookupDto(
    string Id,
    IReadOnlyList<int>? MvpYears,
    IReadOnlyList<ProfileIdentityDto>? Identities = null,
    IReadOnlyList<ProfileFeedDto>? Feeds = null);

/// <summary>An account on another platform that the content platform ties to the profile.</summary>
public sealed record ProfileIdentityDto(
    string? Platform,
    string? Username,
    string? ProfileUrl);

/// <summary>A feed (blog, podcast, …) that the content platform ties to the profile.</summary>
public sealed record ProfileFeedDto(
    string? Url,
    string? Name);

/// <summary>Response envelope from POST /v1/profiles/verify-links.</summary>
public sealed record VerifyLinksResponseDto(IReadOnlyList<VerifyLinkResultDto>? Data);

/// <summary>What the content platform knows about one link: <c>confirmed</c>, <c>conflict</c>, <c>unknown</c> or <c>unsupported</c>.</summary>
public sealed record VerifyLinkResultDto(
    string? Url,
    string? Status,
    string? Platform);

/// <summary>Response envelope from GET /v1/profiles/{id}/activities.</summary>
public sealed record ProfileActivitiesResponseDto(
    IReadOnlyList<ProfileActivityDto> Data,
    PaginationDto Pagination);

public sealed record ProfileActivityDto(
    string Id,
    string Type,
    string? Platform,
    string? Title,
    string? Url,
    string? Summary,
    string? ImageUrl,
    DateTimeOffset OccurredAt);

public static class CommunityBlogsJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}

/// <summary>Error envelope the content platform returns on a non-success response, e.g. <c>{"error":{"code":"invalid_feed","message":"..."}}</c>.</summary>
public sealed record CommunityBlogsErrorEnvelope(CommunityBlogsErrorDetail? Error);

public sealed record CommunityBlogsErrorDetail(string? Code, string? Message);

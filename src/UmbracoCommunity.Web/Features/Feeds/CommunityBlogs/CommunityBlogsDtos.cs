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

public static class CommunityBlogsJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}

/// <summary>Error envelope the content platform returns on a non-success response, e.g. <c>{"error":{"code":"invalid_feed","message":"..."}}</c>.</summary>
public sealed record CommunityBlogsErrorEnvelope(CommunityBlogsErrorDetail? Error);

public sealed record CommunityBlogsErrorDetail(string? Code, string? Message);

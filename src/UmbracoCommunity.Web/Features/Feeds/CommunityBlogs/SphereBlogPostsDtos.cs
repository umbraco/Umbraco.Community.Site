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

public static class SphereJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}

/// <summary>Error envelope Sphere returns on a non-success response, e.g. <c>{"error":{"code":"invalid_feed","message":"..."}}</c>.</summary>
public sealed record SphereErrorEnvelope(SphereErrorDetail? Error);

public sealed record SphereErrorDetail(string? Code, string? Message);

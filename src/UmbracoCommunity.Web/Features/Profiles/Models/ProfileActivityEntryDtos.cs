using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>A piece of activity the member wants to add to their profile. <c>Kind</c> is one of <see cref="ProfileActivityEntry.Kinds"/>.</summary>
public sealed record AddActivityEntryRequest(string? Kind, string? Title, string? Url, DateOnly? Date, string? Description);

/// <summary>An activity entry a member added by hand, with who it belongs to and where it stands.</summary>
public sealed record ActivityEntryRecord(
    int Id,
    string GitHubHandle,
    string DisplayName,
    string Kind,
    string Title,
    string Url,
    DateOnly Date,
    string? Description,
    ActivityEntryState State,
    string? RejectionReason,
    string? DecidedByUserName,
    DateTime? DecidedUtc,
    DateTime CreatedUtc);

public sealed record ActivityEntryPage(IReadOnlyList<ActivityEntryRecord> Items, int Total);

/// <summary>An activity entry as the member sees it on their account page.</summary>
public sealed record MemberActivityEntryResponse(
    int Id,
    string Kind,
    string KindLabel,
    string Title,
    string Url,
    DateOnly Date,
    string? Description,
    string State,
    string? RejectionReason);

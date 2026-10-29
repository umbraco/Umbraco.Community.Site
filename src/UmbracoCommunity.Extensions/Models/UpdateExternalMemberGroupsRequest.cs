using System.ComponentModel.DataAnnotations;

namespace UmbracoCommunity.Extensions.Models;

/// <summary>The complete set of member groups an external-only member should be in.</summary>
public sealed class UpdateExternalMemberGroupsRequest
{
    /// <summary>Keys of every member group the member should be in afterwards; empty removes them from all groups.</summary>
    [Required]
    public Guid[] GroupKeys { get; init; } = [];
}

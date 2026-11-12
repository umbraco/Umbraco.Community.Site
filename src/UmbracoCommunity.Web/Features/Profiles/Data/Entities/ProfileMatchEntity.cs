namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

public enum ProfileMatchState
{
    /// <summary>Waiting for a person to decide.</summary>
    Held = 0,
    Confirmed = 1,
    Rejected = 2
}

public enum ProfileMatchDecider
{
    Automatic = 0,

    /// <summary>A backoffice user decided in the match review dashboard.</summary>
    Manual = 1,

    /// <summary>The member decided on their own profile, e.g. by saying a source is not theirs.</summary>
    Member = 2
}

/// <summary>
/// The decision on whether an account or page found elsewhere (<see cref="SourceUrl"/>) belongs to a member's
/// profile. One row per profile and source. A <see cref="ProfileMatchDecider.Manual"/> or
/// <see cref="ProfileMatchDecider.Member"/> decision is final for automatic runs: they leave the row alone. A
/// backoffice user can still change any decision.
/// </summary>
public class ProfileMatchEntity
{
    /// <summary>Kept short so the (profile, source) unique index fits SQL Server's index key size limit.</summary>
    public const int MaxSourceUrlLength = 450;

    public int Id { get; set; }

    public int MemberProfileId { get; set; }

    public MemberProfileEntity? MemberProfile { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public ProfileMatchState State { get; set; }

    /// <summary>0 to 1.</summary>
    public double Confidence { get; set; }

    /// <summary>JSON array of the signals behind the decision, see <c>MatchSignal</c>.</summary>
    public string SignalsJson { get; set; } = "[]";

    public ProfileMatchDecider DecidedBy { get; set; }

    /// <summary>Key of the backoffice user for a manual decision.</summary>
    public Guid? DecidedByUserKey { get; set; }

    public string? DecidedByUserName { get; set; }

    public DateTime DecidedUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

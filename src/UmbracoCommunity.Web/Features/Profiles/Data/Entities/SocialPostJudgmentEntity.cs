namespace UmbracoCommunity.Web.Features.Profiles.Data.Entities;

/// <summary>
/// A model's answer to "are these two social posts the same post shared twice?", kept so a pair is only ever asked
/// about once. Both answers are kept (the "no"s are most of them), as a probability so a change of threshold needs no new
/// questions. One row per pair of posts, found by <see cref="PairKey"/>.
/// </summary>
public class SocialPostJudgmentEntity
{
    public const int MaxPairKeyLength = 300;

    public int Id { get; set; }

    /// <summary>The two posts' activity ids, in order, joined by "|" (see <c>ProfileTimeline.PairKey</c>).</summary>
    public string PairKey { get; set; } = string.Empty;

    /// <summary>The model's probability (0 to 1) that the two are the same post.</summary>
    public double Probability { get; set; }

    public DateTime JudgedUtc { get; set; }
}

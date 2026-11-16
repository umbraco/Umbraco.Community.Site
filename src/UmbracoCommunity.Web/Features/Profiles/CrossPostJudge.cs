#pragma warning disable UMBRACOAI_DECISION
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.AI.Core.Decision;
using Umbraco.Cms.Core.Cache;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Features.Profiles;

/// <summary>
/// What a judge made of a list of pairs of posts: the pair keys (see <c>ProfileTimeline.PairKey</c>) that are the same
/// post, how many of the <paramref name="Total"/> pairs have an answer, and whether the model couldn't be reached (the
/// pairs without an answer then stay unmerged until it can).
/// </summary>
public sealed record CrossPostJudgment(IReadOnlySet<string> Same, int Answered, int Total, bool ModelUnavailable = false)
{
    public static CrossPostJudgment None { get; } = new(new HashSet<string>(), 0, 0);

    /// <summary>True when some pairs wait for an answer the model could give (it is reachable), so another go would make progress.</summary>
    public bool HasMoreToAsk => !ModelUnavailable && Answered < Total;
}

/// <summary>Decides which pairs of social posts are the same post shared twice.</summary>
public interface ICrossPostJudge
{
    Task<CrossPostJudgment> JudgeAsync(
        IReadOnlyList<(ProfileTimelineEntry A, ProfileTimelineEntry B)> pairs,
        CancellationToken cancellationToken);
}

/// <summary>
/// Asks the default Decision profile (Umbraco.AI, experimental) whether two posts are the same post. It is the only thing
/// that decides this: there is no text comparison in front of it. The answer is a probability; at or above
/// <see cref="ProfileActivityOptions.AiCrossPostThreshold"/> counts as the same. Every answer is kept in the database
/// (see <see cref="SocialPostJudgmentStore"/>), so a pair is asked about once, ever. At most
/// <see cref="ProfileActivityOptions.AiCrossPostMaxPairs"/> new questions are asked per call, closest in time first; the
/// rest wait for the next call. A failure is remembered for a few minutes so a missing or broken model isn't called on
/// every profile view. Only public post text is sent.
/// </summary>
public sealed class AiCrossPostJudge : ICrossPostJudge
{
    // One judging run at a time, so two requests for the same profile don't ask the model the same questions twice.
    private static readonly SemaphoreSlim RunLock = new(1, 1);

    private const string FailedCacheKey = "CrossPostJudge:failed";
    private static readonly TimeSpan FailureCacheDuration = TimeSpan.FromMinutes(5);

    private const string Instructions =
        "Two social media posts by the same person are shown. Decide whether they are the same post shared twice, on " +
        "two platforms or on the same one: the same message, possibly shortened, cut off, reworded slightly, or with " +
        "different links, hashtags or emoji. Two different posts on the same topic are not the same post.";

    private readonly IAIDecisionService _decisions;
    private readonly SocialPostJudgmentStore _store;
    private readonly AppCaches _appCaches;
    private readonly IOptionsMonitor<ProfileActivityOptions> _options;
    private readonly ILogger<AiCrossPostJudge> _logger;

    public AiCrossPostJudge(
        IAIDecisionService decisions,
        SocialPostJudgmentStore store,
        AppCaches appCaches,
        IOptionsMonitor<ProfileActivityOptions> options,
        ILogger<AiCrossPostJudge> logger)
    {
        _decisions = decisions;
        _store = store;
        _appCaches = appCaches;
        _options = options;
        _logger = logger;
    }

    public async Task<CrossPostJudgment> JudgeAsync(
        IReadOnlyList<(ProfileTimelineEntry A, ProfileTimelineEntry B)> pairs,
        CancellationToken cancellationToken)
    {
        if (pairs.Count == 0)
        {
            return CrossPostJudgment.None;
        }

        await RunLock.WaitAsync(cancellationToken);
        try
        {
            return await JudgeCoreAsync(pairs, cancellationToken);
        }
        finally
        {
            RunLock.Release();
        }
    }

    private async Task<CrossPostJudgment> JudgeCoreAsync(
        IReadOnlyList<(ProfileTimelineEntry A, ProfileTimelineEntry B)> pairs,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var keys = pairs.Select(p => ProfileTimeline.PairKey(p.A.ActivityId, p.B.ActivityId)).ToList();
        var known = await _store.GetAsync(keys.Where(k => k.Length > 0).ToList(), cancellationToken);
        var same = new HashSet<string>(StringComparer.Ordinal);
        var asked = 0;
        var unanswered = 0;
        var modelFailed = false;
        var modelDown = _appCaches.RuntimeCache.Get(FailedCacheKey) is not null;

        for (var i = 0; i < pairs.Count; i++)
        {
            var key = keys[i];
            if (key.Length == 0)
            {
                continue;
            }

            if (!known.TryGetValue(key, out var probability))
            {
                if (modelDown || asked >= options.AiCrossPostMaxPairs)
                {
                    unanswered++;
                    modelFailed |= modelDown;
                    continue;
                }

                try
                {
                    probability = await AskAsync(pairs[i].A, pairs[i].B, cancellationToken);
                    asked++;
                    await _store.SaveAsync(key, probability, cancellationToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Could not ask the decision model whether two posts are the same; trying again in a few minutes");
                    _appCaches.RuntimeCache.Insert(FailedCacheKey, () => true, FailureCacheDuration);
                    modelDown = true;
                    modelFailed = true;
                    unanswered++;
                    continue;
                }
            }

            if (probability >= options.AiCrossPostThreshold)
            {
                same.Add(key);
            }
        }

        var judgment = new CrossPostJudgment(same, pairs.Count - unanswered, pairs.Count, modelFailed);
        _logger.LogInformation(
            "Cross-post check: {Candidates} candidate pairs, {Known} already answered, {Asked} asked now, {Same} judged the same, {Left} left",
            pairs.Count, known.Count, asked, same.Count, unanswered);
        return judgment;
    }

    private async Task<double> AskAsync(ProfileTimelineEntry a, ProfileTimelineEntry b, CancellationToken cancellationToken)
    {
        var question = new AIBinaryDecisionQuestion
        {
            Instructions = Instructions,
            TrueCriteria = "They are the same post shared twice.",
            FalseCriteria = "They are two different posts.",
        };
        var state = $"Post 1 ({a.Meta}):\n{a.Title}\n\nPost 2 ({b.Meta}):\n{b.Title}";
        var response = await _decisions.AskAsync<AIBinaryDecisionAnswer>(question, state, cancellationToken: cancellationToken);
        _logger.LogDebug("Cross-post check {Probability:0.00}: {A} / {B}", response.Answer.TrueProbability, a.Title, b.Title);
        return response.Answer.TrueProbability;
    }
}

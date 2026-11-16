using Microsoft.EntityFrameworkCore;
using UmbracoCommunity.Web.Features.Profiles.Data.Entities;

namespace UmbracoCommunity.Web.Features.Profiles.Data;

/// <summary>Keeps a model's answers about pairs of social posts (see <see cref="SocialPostJudgmentEntity"/>).</summary>
public class SocialPostJudgmentStore
{
    private readonly IDbContextFactory<MemberProfilesDbContext> _contextFactory;

    public SocialPostJudgmentStore(IDbContextFactory<MemberProfilesDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    /// <summary>The kept probabilities for <paramref name="pairKeys"/>, by key. Pairs never asked about are left out.</summary>
    public async Task<IReadOnlyDictionary<string, double>> GetAsync(IReadOnlyCollection<string> pairKeys, CancellationToken ct = default)
    {
        var result = new Dictionary<string, double>(StringComparer.Ordinal);
        if (pairKeys.Count == 0)
        {
            return result;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);

        // In batches, so the query's parameter list stays small.
        foreach (var batch in pairKeys.Distinct(StringComparer.Ordinal).Chunk(500))
        {
            var rows = await context.SocialPostJudgments.AsNoTracking()
                .Where(j => batch.Contains(j.PairKey))
                .Select(j => new { j.PairKey, j.Probability })
                .ToListAsync(ct);
            foreach (var row in rows)
            {
                result[row.PairKey] = row.Probability;
            }
        }

        return result;
    }

    /// <summary>Keeps the model's answer for a pair (replacing an earlier one).</summary>
    public async Task SaveAsync(string pairKey, double probability, CancellationToken ct = default)
    {
        if (pairKey.Length is 0 or > SocialPostJudgmentEntity.MaxPairKeyLength)
        {
            return;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(ct);
        var row = await context.SocialPostJudgments.FirstOrDefaultAsync(j => j.PairKey == pairKey, ct);
        if (row == null)
        {
            row = new SocialPostJudgmentEntity { PairKey = pairKey };
            context.SocialPostJudgments.Add(row);
        }

        row.Probability = probability;
        row.JudgedUtc = DateTime.UtcNow;

        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another request kept the same pair at the same moment; its answer is just as good.
        }
    }
}

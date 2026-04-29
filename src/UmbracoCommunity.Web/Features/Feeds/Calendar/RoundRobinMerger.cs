namespace UmbracoCommunity.Web.Features.Feeds.Calendar;

public static class RoundRobinMerger
{
    public static IReadOnlyList<T> Merge<T>(IReadOnlyList<IReadOnlyList<T>> sources, int maxItems)
    {
        if (maxItems <= 0 || sources.Count == 0)
        {
            return Array.Empty<T>();
        }

        var result = new List<T>(maxItems);
        var indices = new int[sources.Count];
        var anyTaken = true;

        while (result.Count < maxItems && anyTaken)
        {
            anyTaken = false;
            for (var s = 0; s < sources.Count && result.Count < maxItems; s++)
            {
                var source = sources[s];
                var i = indices[s];
                if (i < source.Count)
                {
                    result.Add(source[i]);
                    indices[s] = i + 1;
                    anyTaken = true;
                }
            }
        }

        return result;
    }
}

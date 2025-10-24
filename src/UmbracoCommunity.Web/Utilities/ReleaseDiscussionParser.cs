using UmbracoCommunity.Web.Features.GitHubSync.Models;
using UmbracoCommunity.Web.Features.ReleaseOverview.Models;

namespace UmbracoCommunity.Web.Utilities;

public class ReleaseDiscussionParser
{
    public ReleaseDiscussionViewModel? ParseReleaseDiscussion(
        GitHubDiscussion discussion,
        Dictionary<string, (int features, int issues, int breaking)> releaseStats)
    {
        var baseInfo = ParseReleaseInfo(discussion);
        if (baseInfo == null)
            return null;

        // Get stats for this release
        var (features, issues, breaking) = releaseStats.GetValueOrDefault(baseInfo.ReleaseLabel, (0, 0, 0));

        return new ReleaseDiscussionViewModel
        {
            Version = baseInfo.Version,
            ReleaseLabel = baseInfo.ReleaseLabel,
            ActualLatestVersion = baseInfo.ActualLatestVersion,
            ReleaseDate = baseInfo.ReleaseDate,
            IsReleaseDateTba = baseInfo.IsReleaseDateTba,
            IsLts = baseInfo.IsLts,
            Description = baseInfo.Description,
            IsAvailableOnNuGet = baseInfo.IsAvailableOnNuGet,
            DiscussionUrl = baseInfo.DiscussionUrl,
            HasPreRelease = baseInfo.HasPreRelease,
            PreReleaseVersion = baseInfo.PreReleaseVersion,
            FeatureCount = features,
            IssueCount = issues,
            BreakingChangesCount = breaking
        };
    }

    public ReleaseInfoViewModel? ParseReleaseInfo(GitHubDiscussion discussion)
    {
        // Find the release label (format: "release/X.Y.Z")
        var releaseLabel =
            discussion.Labels.FirstOrDefault(l => l.StartsWith("release/", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrEmpty(releaseLabel))
            return null;

        // Extract version from label (e.g., "release/16.4.0" -> "16.4.0")
        var version = releaseLabel.Substring("release/".Length);

        // Validate that the version is a valid SemVer version
        if (!SemVerHelper.IsValidSemVer(version))
            return null;

        // Parse release date from body
        DateTime? releaseDate = null;
        bool isTba = true;

        var releaseDatePattern = @"\*\*Release date:\*\*\s*(.+)";
        var match = System.Text.RegularExpressions.Regex.Match(discussion.Body, releaseDatePattern,
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var dateString = match.Groups[1].Value.Trim();

            // Check if it contains "TODO"
            if (dateString.Contains("TODO", StringComparison.OrdinalIgnoreCase))
            {
                // Try to extract date from "TODO (YYYY-MM-DD)" format
                var todoDatePattern = @"TODO\s*\((\d{4}-\d{2}-\d{2})\)";
                var todoMatch = System.Text.RegularExpressions.Regex.Match(dateString, todoDatePattern);
                if (todoMatch.Success)
                {
                    if (DateTime.TryParse(todoMatch.Groups[1].Value, out var parsedDate))
                    {
                        releaseDate = parsedDate;
                        isTba = true;
                    }
                }
            }
            else
            {
                // Try to parse the date directly (YYYY-MM-DD format)
                if (DateTime.TryParse(dateString, out var parsedDate))
                {
                    releaseDate = parsedDate;
                    isTba = false;
                }
            }
        }
        // If no valid date found, keep isTba = true and releaseDate = null

        // Parse LTS status from body
        bool isLts = false;
        var ltsPattern = @"\*\*Long term supported version\*\*\?\s*(Yes|yes)";
        var ltsMatch = System.Text.RegularExpressions.Regex.Match(discussion.Body, ltsPattern);
        if (ltsMatch.Success)
        {
            isLts = true;
        }

        // Extract description (everything after release date until "### Links")
        var description = string.Empty;
        var bodyLines = discussion.Body.Split('\n');
        bool inDescription = false;
        var descriptionLines = new List<string>();

        foreach (var line in bodyLines)
        {
            if (line.Contains("**Release date:**", StringComparison.OrdinalIgnoreCase))
            {
                inDescription = true;
                continue;
            }

            if (inDescription)
            {
                if (line.TrimStart().StartsWith("### Links", StringComparison.OrdinalIgnoreCase))
                    break;

                // Skip LTS line
                if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\*\*Long term supported version\*\*\?"))
                    continue;

                descriptionLines.Add(line);
            }
        }

        description = string.Join("\n", descriptionLines).Trim();

        return new ReleaseInfoViewModel
        {
            Version = version,
            ReleaseLabel = releaseLabel,
            ReleaseDate = releaseDate,
            IsReleaseDateTba = isTba,
            IsLts = isLts,
            Description = description,
            DiscussionUrl = discussion.Url
        };
    }
}

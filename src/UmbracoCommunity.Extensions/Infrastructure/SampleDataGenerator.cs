using UmbracoCommunity.Web.Features.GitHubSync.Models;

namespace UmbracoCommunity.Extensions.Infrastructure;

public class SampleDataGenerator
{
    private static readonly Random _random = new Random(); // Random seed for varied data each time

    private static readonly string[] FirstNames = new[]
    {
        "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth",
        "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen",
        "Christopher", "Nancy", "Daniel", "Lisa", "Matthew", "Betty", "Anthony", "Margaret", "Mark", "Sandra",
        "Donald", "Ashley", "Steven", "Kimberly", "Paul", "Emily", "Andrew", "Donna", "Joshua", "Michelle",
        "Kenneth", "Dorothy", "Kevin", "Carol", "Brian", "Amanda", "George", "Melissa", "Timothy", "Deborah"
    };

    private static readonly string[] LastNames = new[]
    {
        "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez",
        "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin",
        "Lee", "Perez", "Thompson", "White", "Harris", "Sanchez", "Clark", "Ramirez", "Lewis", "Robinson",
        "Walker", "Young", "Allen", "King", "Wright", "Scott", "Torres", "Nguyen", "Hill", "Flores",
        "Green", "Adams", "Nelson", "Baker", "Hall", "Rivera", "Campbell", "Mitchell", "Carter", "Roberts"
    };

    private static readonly string[] BugTitles = new[]
    {
        "Memory leak in {0}",
        "{0} not saving changes",
        "Performance issue with {0}",
        "{0} crashes on large datasets",
        "Null reference exception in {0}",
        "{0} display issue on mobile",
        "Validation error in {0}",
        "{0} causes infinite loop",
        "Race condition in {0}",
        "{0} fails with special characters"
    };

    private static readonly string[] FeatureTitles = new[]
    {
        "Add support for {0}",
        "Implement {0} functionality",
        "Enhance {0} with new options",
        "Add {0} to backoffice",
        "Improve {0} user experience",
        "Add dark mode support to {0}",
        "Enable {0} customization",
        "Add {0} filtering options",
        "Implement {0} search",
        "Add {0} export functionality"
    };

    private static readonly string[] ComponentNames = new[]
    {
        "media picker", "content picker", "block list editor", "block grid editor", "rich text editor",
        "data type editor", "property editor", "document type", "content tree", "media library",
        "user management", "permission system", "workflow engine", "cache manager", "search indexer",
        "language selector", "multi-site manager", "content delivery API", "webhook handler", "custom dashboard",
        "member editor", "form builder", "grid layout", "macro renderer", "package manager",
        "dictionary editor", "relation manager", "redirect handler", "health check", "log viewer"
    };

    private static readonly string[] Repositories = new[] { "Umbraco-CMS", "Umbraco.Forms", "Umbraco.Deploy", "Umbraco.Workflow" };
    private static readonly string[] ReleaseVersions = new[]
    {
        "release/14.0.0", "release/14.1.0", "release/14.2.0", "release/14.3.0", "release/14.3.1",
        "release/15.0.0", "release/15.0.1", "release/15.1.0", "release/15.2.0",
        "release/16.0.0"
    };

    private static readonly string[] IssueStates = new[] { "OPEN", "CLOSED" };
    private static readonly string[] PrStates = new[] { "OPEN", "MERGED", "CLOSED" };
    private static readonly string[] IssueLabels = new[]
    {
        "type/bug", "type/feature", "type/enhancement", "type/documentation", "type/security",
        "state/hq-discussion", "state/hq-review", "state/fixed", "community/pr-welcome",
        "area/property-editors", "area/backoffice", "area/block-editors", "area/content-delivery-api",
        "area/localization", "area/dependencies", "priority/critical", "priority/high"
    };

    public static List<GitHubHqMember> GenerateHqMembers()
    {
        var members = new List<GitHubHqMember>();
        var baseDate = new DateTime(2015, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int i = 0; i < 150; i++)
        {
            var firstName = FirstNames[i % FirstNames.Length];
            var lastName = LastNames[i % LastNames.Length];
            var login = $"{firstName.ToLower()}{lastName.ToLower()}{(i >= FirstNames.Length ? (i / FirstNames.Length).ToString() : "")}";
            var name = $"{firstName} {lastName}";

            var periods = new List<EmploymentPeriod>();

            // 70% currently employed, 30% former employees
            var isCurrentEmployee = _random.Next(100) < 70;
            var startDate = baseDate.AddDays(_random.Next(0, 3650)); // Random date within ~10 years

            if (isCurrentEmployee)
            {
                periods.Add(new EmploymentPeriod { Start = startDate, End = null });
            }
            else
            {
                var endDate = startDate.AddDays(_random.Next(365, 2555)); // Employed for 1-7 years
                periods.Add(new EmploymentPeriod { Start = startDate, End = endDate });
            }

            // 10% chance of having a gap and coming back
            if (_random.Next(100) < 10 && isCurrentEmployee)
            {
                var returnDate = startDate.AddDays(_random.Next(730, 1825)); // Returned after 2-5 years
                periods.Insert(0, new EmploymentPeriod { Start = startDate, End = startDate.AddDays(_random.Next(365, 730)) });
                periods[1] = new EmploymentPeriod { Start = returnDate, End = null };
            }

            members.Add(new GitHubHqMember
            {
                Id = $"MDQ6VXNlcj{100000 + i}",
                Login = login,
                Name = name,
                Periods = periods
            });
        }

        return members;
    }

    public static List<GitHubIssue> GenerateIssues()
    {
        var issues = new List<GitHubIssue>();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var hqMembers = GenerateHqMembers();

        for (int i = 0; i < 7000; i++)
        {
            var repoName = Repositories[_random.Next(Repositories.Length)];
            var repo = new GitHubRepository
            {
                Name = repoName,
                Url = $"https://github.com/umbraco/{repoName}"
            };

            var isBug = _random.Next(100) < 60; // 60% bugs, 40% features
            var component = ComponentNames[_random.Next(ComponentNames.Length)];
            var titleTemplate = isBug ? BugTitles[_random.Next(BugTitles.Length)] : FeatureTitles[_random.Next(FeatureTitles.Length)];
            var title = string.Format(titleTemplate, component);

            var state = IssueStates[_random.Next(IssueStates.Length)];
            var createdAt = baseDate.AddDays(_random.Next(-3650, 340)); // Span ~10 years back
            var updatedAt = createdAt.AddDays(_random.Next(1, 60));

            // 10% HQ members, 90% community members for issues
            var isHqAuthor = _random.Next(100) < 10;
            string authorLogin, authorName;
            if (isHqAuthor)
            {
                var hqMember = hqMembers[_random.Next(hqMembers.Count)];
                authorLogin = hqMember.Login;
                authorName = hqMember.Name ?? authorLogin;
            }
            else
            {
                var firstName = FirstNames[_random.Next(FirstNames.Length)];
                var lastName = LastNames[_random.Next(LastNames.Length)];
                var authorSuffix = _random.Next(1000);
                authorLogin = $"{firstName.ToLower()}{lastName.ToLower()}{authorSuffix}";
                authorName = $"{firstName} {lastName}";
            }

            // Generate 1-3 labels
            var labelCount = _random.Next(1, 4);
            var labels = new List<string>();

            // Add type label
            labels.Add(isBug ? "type/bug" : (i % 3 == 0 ? "type/feature" : "type/enhancement"));

            // Add state label if closed
            if (state == "CLOSED")
            {
                labels.Add("state/fixed");
            }
            else if (_random.Next(100) < 30)
            {
                labels.Add(_random.Next(2) == 0 ? "state/hq-discussion" : "state/hq-review");
            }

            // Add area label
            if (_random.Next(100) < 60)
            {
                var areaLabels = IssueLabels.Where(l => l.StartsWith("area/")).ToArray();
                labels.Add(areaLabels[_random.Next(areaLabels.Length)]);
            }

            // 40% chance of having a release label
            if (_random.Next(100) < 40)
            {
                labels.Add(ReleaseVersions[_random.Next(ReleaseVersions.Length)]);
            }

            issues.Add(new GitHubIssue
            {
                Id = $"I_kKDpvaUJpc3N1ZT{100000 + i}",
                Title = title,
                Number = 14000 + i,
                Url = $"https://github.com/umbraco/{repoName}/issues/{14000 + i}",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                State = state,
                Author = new GitHubAuthor
                {
                    Login = authorLogin,
                    Name = authorName,
                    Url = $"https://github.com/{authorLogin}"
                },
                Repository = repo,
                Labels = labels
            });
        }

        return issues;
    }

    public static List<GitHubPullRequest> GeneratePullRequests()
    {
        var prs = new List<GitHubPullRequest>();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var hqMembers = GenerateHqMembers();

        for (int i = 0; i < 12000; i++)
        {
            var repoName = Repositories[_random.Next(Repositories.Length)];
            var repo = new GitHubRepository
            {
                Name = repoName,
                Url = $"https://github.com/umbraco/{repoName}"
            };

            var isBug = _random.Next(100) < 60;
            var component = ComponentNames[_random.Next(ComponentNames.Length)];
            var titleTemplate = isBug ? BugTitles[_random.Next(BugTitles.Length)] : FeatureTitles[_random.Next(FeatureTitles.Length)];
            var title = string.Format(titleTemplate, component).Replace("Add support for", "Implement").Replace("Implement Implement", "Implement");

            var state = PrStates[_random.Next(PrStates.Length)];
            var createdAt = baseDate.AddDays(_random.Next(-3650, 340)); // Span ~10 years back
            var updatedAt = createdAt.AddDays(_random.Next(1, 30));
            DateTime? mergedAt = state == "MERGED" ? updatedAt.AddHours(_random.Next(1, 48)) : null;

            // Author: ~80% HQ members, ~20% community members for PRs
            var isHqAuthor = _random.Next(100) < 80;
            string authorLogin, authorName;
            if (isHqAuthor)
            {
                var hqMember = hqMembers[_random.Next(hqMembers.Count)];
                authorLogin = hqMember.Login;
                authorName = hqMember.Name ?? authorLogin;
            }
            else
            {
                var authorFirstName = FirstNames[_random.Next(FirstNames.Length)];
                var authorLastName = LastNames[_random.Next(LastNames.Length)];
                var authorSuffix = _random.Next(1000);
                authorLogin = $"{authorFirstName.ToLower()}{authorLastName.ToLower()}{authorSuffix}";
                authorName = $"{authorFirstName} {authorLastName}";
            }

            // Merger (if merged)
            GitHubAuthor? mergedBy = null;
            if (state == "MERGED")
            {
                var merger = hqMembers[_random.Next(hqMembers.Count)]; // Pick any HQ member as merger
                mergedBy = new GitHubAuthor
                {
                    Login = merger.Login,
                    Name = merger.Name,
                    Url = $"https://github.com/{merger.Login}"
                };
            }

            var labels = new List<string>();
            labels.Add(isBug ? "type/bug" : "type/feature");

            if (state == "MERGED" && _random.Next(100) < 70)
            {
                labels.Add(ReleaseVersions[_random.Next(ReleaseVersions.Length)]);
            }
            else if (state == "OPEN" && _random.Next(100) < 50)
            {
                labels.Add("state/hq-review");
                labels.Add(ReleaseVersions[_random.Next(Math.Max(1, ReleaseVersions.Length - 3), ReleaseVersions.Length)]); // Newer releases
            }

            if (_random.Next(100) < 30)
            {
                var areaLabels = IssueLabels.Where(l => l.StartsWith("area/")).ToArray();
                labels.Add(areaLabels[_random.Next(areaLabels.Length)]);
            }

            prs.Add(new GitHubPullRequest
            {
                Id = $"PR_kKDpvaUJyMT{200000 + i}",
                Title = title,
                Number = 16000 + i,
                Url = $"https://github.com/umbraco/{repoName}/pull/{16000 + i}",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                MergedAt = mergedAt,
                State = state,
                Author = new GitHubAuthor
                {
                    Login = authorLogin,
                    Name = authorName,
                    Url = $"https://github.com/{authorLogin}"
                },
                MergedBy = mergedBy,
                Repository = repo,
                Labels = labels
            });
        }

        return prs;
    }

    public static List<GitHubDiscussion> GenerateDiscussions()
    {
        var discussions = new List<GitHubDiscussion>();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var releaseTemplates = new[]
        {
            ("14.0.0", "May 30, 2024", "Yes (until May 2027)", "Major release with breaking changes"),
            ("14.1.0", "July 10, 2024", "Yes (until May 2027)", "Minor enhancements and bug fixes"),
            ("14.2.0", "September 24, 2024", "Yes (until May 2027)", "Localization improvements"),
            ("14.3.0", "November 8, 2024", "Yes (until May 2027)", "Block editor stability"),
            ("15.0.0", "December 12, 2024", "No", ".NET 9 upgrade"),
            ("15.1.0", "TBD", "No", "UI improvements"),
            ("15.2.0", "TBD", "No", "API enhancements"),
            ("16.0.0", "TBD (Q2 2025)", "No", "Major UI overhaul")
        };

        for (int i = 0; i < 50; i++)
        {
            var repoName = i < 40 ? "Umbraco-CMS" : Repositories[_random.Next(Repositories.Length)];
            var repo = new GitHubRepository
            {
                Name = repoName,
                Url = $"https://github.com/umbraco/{repoName}"
            };

            string title, body, releaseLabel;

            if (i < releaseTemplates.Length)
            {
                var template = releaseTemplates[i];
                title = $"Umbraco {template.Item1}";
                releaseLabel = $"release/{template.Item1}";
                body = $@"## Release Information

**Release Date:** {template.Item2}
**LTS:** {template.Item3}

## What's New

- {template.Item4}
- Performance improvements
- Bug fixes and stability enhancements

## Bug Fixes

- Various bug fixes
- Improved error handling";
            }
            else
            {
                // Generate patch releases and other discussions
                var majorVersion = 14 + (_random.Next(3));
                var minorVersion = _random.Next(5);
                var patchVersion = _random.Next(10);
                var version = $"{majorVersion}.{minorVersion}.{patchVersion}";

                title = $"Umbraco {version}";
                releaseLabel = $"release/{version}";
                body = $@"## Release Information

**Release Date:** {baseDate.AddDays(_random.Next(-180, 180)).ToString("MMMM dd, yyyy")}
**LTS:** {(majorVersion == 14 ? "Yes" : "No")}

## Changes

- Bug fixes
- Performance improvements
- Security updates";
            }

            var createdAt = baseDate.AddDays(_random.Next(-365, 0));
            var updatedAt = createdAt.AddDays(_random.Next(1, 90));

            discussions.Add(new GitHubDiscussion
            {
                Id = $"D_kKDpvaURpc2N1c3Npb24={300000 + i}",
                Title = title,
                Number = 5000 + i,
                Url = $"https://github.com/umbraco/{repoName}/discussions/{5000 + i}",
                Body = body,
                CreatedAt = createdAt,
                UpdatedAt = updatedAt,
                Repository = repo,
                Labels = new List<string> { releaseLabel },
                CategoryId = "DIC_kwDOAF7aTM4B-Khv",
                CategoryName = "Releases"
            });
        }

        return discussions;
    }

    public static Dictionary<string, Dictionary<string, DateTime>> GenerateNuGetPackageVersions()
    {
        var packages = new Dictionary<string, Dictionary<string, DateTime>>();
        var baseDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var packageNames = new[]
        {
            "Umbraco.Cms", "Umbraco.Forms", "Umbraco.Deploy", "Umbraco.Workflow", "Umbraco.Commerce"
        };

        foreach (var packageName in packageNames)
        {
            var versions = new Dictionary<string, DateTime>();

            // Generate versions 13.x
            for (int minor = 0; minor < 5; minor++)
            {
                for (int patch = 0; patch < 3; patch++)
                {
                    var version = $"13.{minor}.{patch}";
                    var publishDate = baseDate.AddDays(-365 + (minor * 60) + (patch * 15));
                    versions[version] = publishDate;
                }
            }

            // Generate versions 14.x
            for (int minor = 0; minor < 4; minor++)
            {
                for (int patch = 0; patch < 2; patch++)
                {
                    var version = $"14.{minor}.{patch}";
                    var publishDate = baseDate.AddDays(-180 + (minor * 50) + (patch * 15));
                    versions[version] = publishDate;
                }
            }

            // Generate versions 15.x
            for (int minor = 0; minor < 2; minor++)
            {
                var version = $"15.{minor}.0";
                var publishDate = baseDate.AddDays(-30 + (minor * 30));
                versions[version] = publishDate;
            }

            packages[packageName] = versions;
        }

        return packages;
    }
}

namespace UmbracoCommunity.Web.Features.GitHubSync.Models;

public class GitHubHqMember
{
    public int Id { get; set; }
    public string Login { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public List<EmploymentPeriod> Periods { get; set; } = new();
}

public class EmploymentPeriod
{
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
}

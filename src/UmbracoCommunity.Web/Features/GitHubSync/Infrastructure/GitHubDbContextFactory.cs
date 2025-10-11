using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UmbracoCommunity.Web.Features.GitHubSync.Infrastructure;

public class GitHubDbContextFactory : IDesignTimeDbContextFactory<GitHubDbContext>
{
    public GitHubDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<GitHubDbContext>();

        // Use a placeholder connection string for design-time migrations
        // At runtime, the actual connection string from umbracoDbDSN will be used
        optionsBuilder.UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=UmbracoCommunity;Trusted_Connection=True;");

        return new GitHubDbContext(optionsBuilder.Options);
    }
}

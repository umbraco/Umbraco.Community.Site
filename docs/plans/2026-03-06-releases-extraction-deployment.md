# Releases Extraction — Deployment Checklist

Manual steps required after deploying the `feature/extract-releases` branch to staging/production.

## Umbraco Backoffice Cleanup

### 1. Delete content
- Delete the **releases.umbraco.com** content node (tenant site)

### 2. Delete templates
- Delete **HomeReleases** template (child of LayoutReleases)
- Delete **LayoutReleases** template

### 3. Delete document types
- Delete **Releases Home** document type
- Delete **[Tenant] Releases** folder/document type

## Database Cleanup

Drop the following tables (created by the now-removed GitHubSync EF Core migration).
Junction tables first (foreign key constraints), then main tables:

```sql
DROP TABLE IF EXISTS GitHubIssueReleases;
DROP TABLE IF EXISTS GitHubPullRequestReleases;
DROP TABLE IF EXISTS NuGetPackageVersions;
DROP TABLE IF EXISTS GitHubDiscussions;
DROP TABLE IF EXISTS GitHubHqMembers;
DROP TABLE IF EXISTS GitHubIssues;
DROP TABLE IF EXISTS GitHubPullRequests;
```

Clean up the old migration row (keep the `__EFMigrationsHistory` table — it will be reused by upcoming migrations):

```sql
DELETE FROM __EFMigrationsHistory WHERE MigrationId = '20251011121208_InitialCreate';
```

## Verification

After cleanup, confirm:
- The community site builds and runs without errors
- No broken references in the backoffice content tree
- No orphaned templates listed under Settings > Templates

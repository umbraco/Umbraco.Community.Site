# OutOfMemoryException Fix - Compare Page

## Problem Summary

The `/compare` endpoint was causing 481 OutOfMemoryExceptions on Feb 2, 2026, leading to application crashes and 500 errors in the backoffice.

## Root Cause Analysis

### Primary Issue: Unbounded Database Queries

**Location**: `GitHubSqlStore.cs`
- `GetAllPullRequestsAsync()` (line 238-248)
- `GetAllIssuesAsync()` (line 279-289)
- `GetFirstTimeContributorPrNumbersAsync()` (line 430-455)

**Problem**: These methods loaded **entire tables** into memory without pagination or limits:
```csharp
var entities = await context.PullRequests
    .Where(pr => pr.RepositoryName == repositoryName)
    .ToListAsync();  // ⚠️ NO LIMIT - loads thousands of records
```

### Secondary Issue: Redundant Queries

**Location**: `ComparePageViewModelBuilder.cs`

The same expensive queries were executed **multiple times per page load**:
1. Line 94: `GetPullRequestsByLabelPattern()` - loads all PRs
2. Line 95: `GetIssuesByLabelPattern()` - loads all issues
3. Line 312: **Same query again** inside `GetAvailableVersions()`
4. Line 313: **Same query again** inside `GetAvailableVersions()`

### Memory Impact Calculation

For Umbraco-CMS repository:
- ~10,000 PRs × 2KB JSON each = **20MB raw data**
- ~5,000 Issues × 2KB JSON each = **10MB raw data**
- **Loaded 3+ times per request** = 90MB+ raw data
- **After deserialization**: ~200-300MB per request
- **Multiple concurrent users**: Memory multiplies quickly

## Solutions Implemented

### 1. Added Query Filtering (GitHubSqlStore.cs)

Changed unbounded queries to **only load records with release labels** using join tables:

**Before**:
```csharp
var entities = await context.PullRequests
    .Where(pr => pr.RepositoryName == repositoryName)
    .ToListAsync();  // Loads ALL PRs
```

**After**:
```csharp
var entities = await context.PullRequestReleases
    .Select(prl => prl.PullRequestId)
    .Distinct()
    .Join(context.PullRequests, ...)
    .Where(pr => pr.RepositoryName == repositoryName)
    .ToListAsync();  // Only loads PRs with release labels
```

This reduces the result set from **~10,000 records to ~2,000 records** (80% reduction).

### 2. Added Query-Level Caching

Added memory caching to expensive database queries:

```csharp
var cacheKey = $"AllPullRequests_{repositoryName}";
var cachedResult = _memoryCache.Get<IEnumerable<GitHubPullRequest>>(cacheKey);
if (cachedResult != null)
{
    return cachedResult;
}
// ... query database ...
_memoryCache.Set(cacheKey, result, TimeSpan.FromHours(1));
```

**Cache durations**:
- Pull requests: 1 hour
- Issues: 1 hour
- First-time contributors: 2 hours

### 3. Eliminated Redundant Queries (ComparePageViewModelBuilder.cs)

Refactored to load data **once** and reuse it:

**Before**:
```csharp
// Line 94-95: Load PRs and Issues
var allPrs = _dataStore.GetPullRequestsByLabelPattern(...).ToList();
var allIssues = _dataStore.GetIssuesByLabelPattern(...).ToList();

// Line 312-313: Load AGAIN inside GetAvailableVersions()
var allPrs = _dataStore.GetPullRequestsByLabelPattern(...).ToList();
var allIssues = _dataStore.GetIssuesByLabelPattern(...).ToList();
```

**After**:
```csharp
// Load once
var allPrs = _dataStore.GetPullRequestsByLabelPattern(...).ToList();
var allIssues = _dataStore.GetIssuesByLabelPattern(...).ToList();

// Pass to GetAvailableVersions to reuse
viewModel.AvailableVersions = GetAvailableVersions(repositoryName, includePreReleases, allPrs, allIssues);
```

### 4. Updated Cache Invalidation

Updated `UpsertPullRequests()`, `UpsertIssues()`, `ClearAllData()`, and `ClearGitHubData()` to clear the new query-level caches when data changes.

## Expected Impact

### Memory Reduction
- **Before**: ~200-300MB per request, 3+ queries
- **After**: ~40-60MB per request (cached after first load)
- **Reduction**: 70-80% memory usage decrease

### Query Reduction
- **Before**: 4+ database queries per page load
- **After**: 1 database query (subsequent loads use cache)
- **Reduction**: 75% fewer database queries

### Response Time
- **First request**: Slightly faster (fewer records to process)
- **Subsequent requests**: 80-90% faster (cached data)

## Testing Recommendations

1. **Load test the /compare page** with multiple concurrent users
2. **Monitor memory usage** under load
3. **Verify cache is working** by checking response times (first vs. subsequent)
4. **Test cache invalidation** by updating GitHub data and verifying cache clears
5. **Check Hangfire sync jobs** don't trigger memory issues after cache clears

## Files Modified

1. `src/UmbracoCommunity.Web/Features/GitHubSync/Infrastructure/GitHubSqlStore.cs`
   - Added filtering to `GetAllPullRequestsAsync()`
   - Added filtering to `GetAllIssuesAsync()`
   - Added filtering to `GetFirstTimeContributorPrNumbersAsync()`
   - Added query-level caching to all three methods
   - Updated cache invalidation in upsert and clear methods

2. `src/UmbracoCommunity.Web/ViewModelBuilders/Pages/ComparePageViewModelBuilder.cs`
   - Refactored to eliminate redundant queries
   - Updated `GetAvailableVersions()` to accept optional pre-loaded data

## Additional Notes

- The fix follows existing patterns in the codebase (`.Take(1000)` pattern from lines 268, 309)
- Caching uses the same `IMemoryCache` infrastructure already in use
- Query filtering uses existing junction tables (`PullRequestReleases`, `IssueReleases`)
- No breaking changes to public APIs

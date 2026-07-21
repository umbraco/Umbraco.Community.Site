using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;
using UmbracoCommunity.Web.Features.Profiles.Data;

namespace UmbracoCommunity.Web.Features.Profiles;

public sealed record AvatarUploadResult(bool Succeeded, string? Error, Guid? MediaKey)
{
    public static AvatarUploadResult Failure(string error) => new(false, error, null);
    public static AvatarUploadResult Success(Guid mediaKey) => new(true, null, mediaKey);
}

/// <summary>
/// Validates and stores a member-uploaded avatar as an Umbraco Media item under
/// Community/Profiles/Avatars (created on first use, folder id then cached). Media library
/// storage (rather than a bespoke blob store) means the image is served from 'self' with no
/// CSP or signed-proxy machinery needed — unlike <c>FeedSubmissionImageProxyService</c>,
/// which exists specifically because that content lives on an external domain.
/// </summary>
public class AvatarUploadService
{
    // Matches FeedSubmissionImageProxyService's order-of-magnitude size cap. Internal (not
    // private) so ProfileApiController's [RequestSizeLimit] can share this single source of
    // truth instead of duplicating the number.
    internal const long MaxBytes = 5_000_000;

    private static readonly string[] AvatarsFolderPath = ["Community", "Profiles", "Avatars"];
    private const string FolderCacheKey = "avatar-upload::folder-id";
    private static readonly TimeSpan FolderCacheTtl = TimeSpan.FromHours(6);

    private static readonly HashSet<string> AllowedExtensions =
        Constants.Security.AllowedImageTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(e => "." + e.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private readonly IMediaService _mediaService;
    private readonly MediaFileManager _mediaFileManager;
    private readonly MediaUrlGeneratorCollection _mediaUrlGenerators;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IContentTypeBaseServiceProvider _contentTypeBaseServiceProvider;
    private readonly AppCaches _appCaches;
    private readonly MemberProfileStore _store;

    public AvatarUploadService(
        IMediaService mediaService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        AppCaches appCaches,
        MemberProfileStore store)
    {
        _mediaService = mediaService;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
        _appCaches = appCaches;
        _store = store;
    }

    public async Task<AvatarUploadResult> UploadAsync(
        Guid memberKey,
        string fileName,
        long length,
        Stream content,
        CancellationToken cancellationToken = default)
    {
        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
        {
            return AvatarUploadResult.Failure("Please upload a GIF, JPEG, PNG, or WEBP image.");
        }

        if (length <= 0 || length > MaxBytes)
        {
            return AvatarUploadResult.Failure($"Image must be {MaxBytes / 1_000_000}MB or smaller.");
        }

        var previousMediaKey = (await _store.GetByMemberKeyAsync(memberKey, cancellationToken))?.AvatarMediaKey;

        var media = _mediaService.CreateMedia($"avatar-{memberKey}", GetOrCreateAvatarsFolderId(), Umbraco.Cms.Core.Constants.Conventions.MediaTypes.Image);
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, "umbracoFile", fileName, content);
        _mediaService.Save(media);

        await _store.UpdateAvatarAsync(memberKey, media.Key, cancellationToken);

        // Replaces rather than orphans the prior upload — otherwise every re-upload during
        // onboarding (a common "let me try a different photo" flow) left a dead media item
        // behind, since MemberProfileEntity.AvatarMediaKey only ever points at the latest one.
        DeleteMediaIfExists(previousMediaKey);

        return AvatarUploadResult.Success(media.Key);
    }

    /// <summary>Clears the member's custom avatar so display falls back to their GitHub default.</summary>
    public async Task RemoveAsync(Guid memberKey, CancellationToken cancellationToken = default)
    {
        var previousMediaKey = (await _store.GetByMemberKeyAsync(memberKey, cancellationToken))?.AvatarMediaKey;
        if (previousMediaKey is null)
        {
            return;
        }

        await _store.UpdateAvatarAsync(memberKey, null, cancellationToken);
        DeleteMediaIfExists(previousMediaKey);
    }

    private void DeleteMediaIfExists(Guid? mediaKey)
    {
        if (mediaKey is not { } key)
        {
            return;
        }

        var media = _mediaService.GetById(key);
        if (media != null)
        {
            _mediaService.Delete(media);
        }
    }

    /// <summary>
    /// Resolves the Community/Profiles/Avatars media folder, creating any missing segment on
    /// first use. The path never changes at runtime, so the resolved id is cached rather than
    /// re-walked on every single upload.
    /// </summary>
    private int GetOrCreateAvatarsFolderId()
    {
        return _appCaches.RuntimeCache.GetCacheItem(
            FolderCacheKey,
            () =>
            {
                var parentId = Umbraco.Cms.Core.Constants.System.Root;
                foreach (var segment in AvatarsFolderPath)
                {
                    parentId = FindOrCreateFolder(segment, parentId);
                }

                return parentId;
            },
            timeout: FolderCacheTtl);
    }

    private int FindOrCreateFolder(string name, int parentId)
    {
        var children = _mediaService.GetPagedChildren(parentId, 0, 500, out _);
        var existing = children.FirstOrDefault(c =>
            c.ContentType.Alias == Umbraco.Cms.Core.Constants.Conventions.MediaTypes.Folder &&
            string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            return existing.Id;
        }

        var folder = _mediaService.CreateMedia(name, parentId, Umbraco.Cms.Core.Constants.Conventions.MediaTypes.Folder);
        _mediaService.Save(folder);
        return folder.Id;
    }
}

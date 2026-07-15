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
/// Validates and stores a member-uploaded avatar as an Umbraco Media item. Media library
/// storage (rather than a bespoke blob store) means the image is served from 'self' with no
/// CSP or signed-proxy machinery needed — unlike <c>FeedSubmissionImageProxyService</c>,
/// which exists specifically because that content lives on an external domain.
/// </summary>
public class AvatarUploadService
{
    // Matches FeedSubmissionImageProxyService's order-of-magnitude size cap.
    private const long MaxBytes = 5_000_000;

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
    private readonly MemberProfileStore _store;

    public AvatarUploadService(
        IMediaService mediaService,
        MediaFileManager mediaFileManager,
        MediaUrlGeneratorCollection mediaUrlGenerators,
        IShortStringHelper shortStringHelper,
        IContentTypeBaseServiceProvider contentTypeBaseServiceProvider,
        MemberProfileStore store)
    {
        _mediaService = mediaService;
        _mediaFileManager = mediaFileManager;
        _mediaUrlGenerators = mediaUrlGenerators;
        _shortStringHelper = shortStringHelper;
        _contentTypeBaseServiceProvider = contentTypeBaseServiceProvider;
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
            return AvatarUploadResult.Failure("Unsupported image type.");
        }

        if (length <= 0 || length > MaxBytes)
        {
            return AvatarUploadResult.Failure("Image is too large.");
        }

        // Each upload creates a new media item rather than reusing/deleting a prior one —
        // IMediaService only exposes GetById(int), so resolving the previous item from the
        // stored Guid key would need an extra entity-service lookup. Left as a known
        // simplification: orphaned prior avatars are a minor housekeeping cost, not a
        // correctness issue, since MemberProfileEntity.AvatarMediaKey always points at the
        // current one.
        var media = _mediaService.CreateMedia($"avatar-{memberKey}", -1, "Image");
        media.SetValue(_mediaFileManager, _mediaUrlGenerators, _shortStringHelper, _contentTypeBaseServiceProvider, "umbracoFile", fileName, content);
        _mediaService.Save(media);

        await _store.UpdateAvatarAsync(memberKey, media.Key, cancellationToken);

        return AvatarUploadResult.Success(media.Key);
    }
}

using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Mvp.Infrastructure;
using UmbracoCommunity.Web.Features.Mvp.Models;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.Models;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// The picture a member can attach to activity they add by hand: what is accepted and how it is stored, that the entry
/// and the file never get out of step (when adding, editing, withdrawing and removing), and that the entry's timeline
/// card shows it.
/// </summary>
public class ProfileActivityEntryImageTests : IDisposable
{
    private const string Handle = "octocat";
    private const string Link = "https://example.dev/my-post";

    private static readonly byte[] Png = ProfileTimelineImageStoreTests.Png;
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];
    private static readonly byte[] Gif = "GIF89a..."u8.ToArray();
    private static readonly byte[] Webp = [.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8, 1];

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly InMemoryMediaFileSystem _media = new();
    private readonly MemberProfileStore _store;
    private readonly ProfileActivityImageStore _images;
    private readonly ProfileActivityEntryService _activityEntryService;
    private readonly Mock<IProfileSyncClient> _syncClient = new();
    private readonly Guid _memberKey = Guid.NewGuid();

    public ProfileActivityEntryImageTests()
    {
        _store = new MemberProfileStore(_database);
        _images = new ProfileActivityImageStore(_media.FileSystem, NullLogger<ProfileActivityImageStore>.Instance);
        _activityEntryService = new ProfileActivityEntryService(
            new MemberActivityEntryStore(_database),
            _syncClient.Object,
            _images,
            Caches(),
            NullLogger<ProfileActivityEntryService>.Instance);

        using var context = _database.CreateDbContext();
        context.Database.Migrate();
    }

    public void Dispose() => _database.Dispose();

    private static AppCaches Caches() => new(new ObjectCacheAppCache(), NoAppCache.Instance, new IsolatedCaches(_ => NoAppCache.Instance));

    private async Task CreateProfileAsync()
    {
        await _store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        await _store.CompleteOnboardingAsync(_memberKey);
    }

    private ProfileActivityEntryApiController CreateController()
    {
        var member = new MemberIdentityUser { Key = _memberKey, UserName = Handle };
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        memberManager.Setup(m => m.GetRolesAsync(member)).ReturnsAsync(new List<string> { ProfileAccessService.EarlyAccessGroupName });

        var provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>(), Mock.Of<Umbraco.Cms.Core.Media.IImageUrlGenerator>()),
            TestImageProxy.Create());
        var access = new ProfileAccessService(memberManager.Object, provider, NullLogger<ProfileAccessService>.Instance);
        return new ProfileActivityEntryApiController(
            TestProfileEditSubjects.Resolver(memberManager.Object, access, _store), _store, access, _activityEntryService, _images, ProfileActivityLinkLookupTests.Create(_database, _media));
    }

    private ProfileActivityService CreateActivityService() =>
        new(
            _syncClient.Object,
            _activityEntryService,
            new ProfileMatchStore(_database),
            Mock.Of<IMvpDataService>(m => m.GetAll() == Array.Empty<MvpYear>()),
            Caches(),
            Mock.Of<IOptionsMonitor<ProfileActivityOptions>>(o => o.CurrentValue == new ProfileActivityOptions()),
            NullLogger<ProfileActivityService>.Instance);

    private static AddActivityEntryForm Form(IFormFile? image = null, string url = Link, string kind = "blog", string? date = "2026-10-01") =>
        new() { Kind = kind, Title = "My post", Url = url, Date = date, Description = "About a thing.", Image = image };

    private static IFormFile Upload(byte[] bytes, string fileName = "../../evil name.png") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "image", fileName);

    private static MemberActivityEntryResponse Added(IActionResult result) =>
        result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<MemberActivityEntryResponse>().Subject;

    private static string? Error(IActionResult result) =>
        result.Should().BeAssignableTo<ObjectResult>().Which.Value!.GetType().GetProperty("error")!.GetValue(
            ((ObjectResult)result).Value) as string;

    private async Task<string?> StoredImagePathAsync(int entryId)
    {
        await using var context = _database.CreateDbContext();
        return (await context.MemberActivityEntries.SingleAsync(e => e.Id == entryId)).ImagePath;
    }

    private IEnumerable<string> StoredPictures => _media.Files.Keys.Where(k => k.StartsWith(ProfileActivityImageStore.Folder + "/", StringComparison.Ordinal));

    // ---- what is accepted, and how it is stored ----

    public static TheoryData<byte[], string> Formats() => new()
    {
        { Png, ".png" },
        { Jpeg, ".jpg" },
        { Gif, ".gif" },
        { Webp, ".webp" },
    };

    [Theory]
    [MemberData(nameof(Formats))]
    public async Task An_accepted_picture_is_stored_under_a_random_name_with_the_extension_of_its_bytes(byte[] bytes, string extension)
    {
        var (path, error) = await _images.SaveAsync(new MemoryStream(bytes), bytes.Length);

        error.Should().BeNull();
        path.Should().MatchRegex($@"^{ProfileActivityImageStore.Folder}/[0-9a-f]{{32}}\{extension}$");
        _media.Files.Should().ContainKey(path!).WhoseValue.Should().Equal(bytes);
        _images.Url(path!).Should().Be("/media/" + path);
    }

    [Fact]
    public async Task Two_uploads_of_the_same_picture_get_different_names()
    {
        var (first, _) = await _images.SaveAsync(new MemoryStream(Png), Png.Length);
        var (second, _) = await _images.SaveAsync(new MemoryStream(Png), Png.Length);

        first.Should().NotBe(second);
    }

    [Fact]
    public async Task Anything_that_is_not_an_accepted_image_is_refused_whatever_it_is_called()
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>"u8.ToArray();

        var (path, error) = await _images.SaveAsync(new MemoryStream(svg), svg.Length);

        path.Should().BeNull();
        error.Should().Contain("JPEG, PNG, GIF or WebP");
        _media.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task A_picture_over_the_limit_is_refused_by_its_stated_size_or_by_what_is_read()
    {
        var big = new byte[ProfileActivityEntry.MaxImageBytes + 1];
        Png.CopyTo(big, 0);

        (await _images.SaveAsync(new MemoryStream(big), big.Length)).Error.Should().Contain("2 MB");
        // An upload that says it is small but isn't.
        (await _images.SaveAsync(new MemoryStream(big), 10)).Error.Should().Contain("2 MB");
        (await _images.SaveAsync(new MemoryStream([]), 0)).Error.Should().NotBeNull();
        _media.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task A_picture_of_exactly_the_limit_is_accepted()
    {
        var exact = new byte[ProfileActivityEntry.MaxImageBytes];
        Png.CopyTo(exact, 0);

        (await _images.SaveAsync(new MemoryStream(exact), exact.Length)).Path.Should().NotBeNull();
    }

    [Fact]
    public void The_request_limit_leaves_room_for_the_largest_picture_and_the_fields()
    {
        ProfileActivityEntry.MaxAddRequestBytes.Should().BeGreaterThan(
            ProfileActivityEntry.MaxImageBytes + ProfileActivityEntry.MaxTitleLength + ProfileActivityEntry.MaxDescriptionLength + 2048);
    }

    [Fact]
    public void A_date_that_is_not_yyyy_MM_dd_is_reported_as_missing()
    {
        var request = Form(date: "01/10/2026").ToRequest();

        request.Date.Should().BeNull();
        ProfileActivityEntry.TryNormalize(request, new DateOnly(2026, 11, 9), out _, out var error).Should().BeFalse();
        error.Should().Contain("date");
        Form(date: " 2026-10-01 ").ToRequest().Date.Should().Be(new DateOnly(2026, 10, 1));
    }

    // ---- adding through the member API ----

    [Fact]
    public async Task An_entry_added_with_a_picture_keeps_it_and_returns_its_address()
    {
        await CreateProfileAsync();

        var added = Added(await CreateController().Add(Form(Upload(Png))));

        var path = await StoredImagePathAsync(added.Id);
        path.Should().StartWith(ProfileActivityImageStore.Folder + "/").And.EndWith(".png").And.NotContain("evil");
        added.ImageUrl.Should().Be("/media/" + path);
        _media.Files.Should().ContainKey(path!);
    }

    [Fact]
    public async Task An_entry_without_a_picture_has_none()
    {
        await CreateProfileAsync();

        var added = Added(await CreateController().Add(Form()));

        added.ImageUrl.Should().BeNull();
        (await StoredImagePathAsync(added.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A_bad_picture_is_a_400_with_a_message_and_stores_nothing()
    {
        await CreateProfileAsync();
        var controller = CreateController();

        var result = await controller.Add(Form(Upload("not a picture"u8.ToArray(), "photo.jpg")));

        result.Should().BeOfType<BadRequestObjectResult>();
        Error(result).Should().Contain("JPEG, PNG, GIF or WebP");
        _media.Files.Should().BeEmpty();
        (await _activityEntryService.ListForMemberAsync((await _store.GetByMemberKeyAsync(_memberKey))!.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task A_bad_entry_with_a_picture_stores_neither()
    {
        await CreateProfileAsync();

        (await CreateController().Add(Form(Upload(Png), url: "http://example.dev/a"))).Should().BeOfType<BadRequestObjectResult>();

        _media.Files.Should().BeEmpty();
    }

    [Fact]
    public async Task When_the_entry_cannot_be_added_its_picture_is_deleted_again()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        Added(await controller.Add(Form(Upload(Png))));
        var kept = StoredPictures.ToList();

        // The same link again: refused after the picture was stored.
        (await controller.Add(Form(Upload(Jpeg)))).Should().BeOfType<ConflictObjectResult>();

        StoredPictures.Should().BeEquivalentTo(kept);
    }

    [Fact]
    public async Task When_adding_fails_the_picture_is_deleted_again()
    {
        var (path, _) = await _images.SaveAsync(new MemoryStream(Png), Png.Length);
        var cancelled = new CancellationToken(canceled: true);

        var adding = () => _activityEntryService.AddAsync(1, new AddActivityEntryRequest("blog", "T", Link, new DateOnly(2026, 10, 1), null), path, cancelled);

        await adding.Should().ThrowAsync<OperationCanceledException>();
        _media.Files.Should().NotContainKey(path!);
    }

    // ---- withdrawing and rejecting ----

    [Fact]
    public async Task Withdrawing_an_entry_deletes_its_picture_and_the_entry_stops_pointing_at_it()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var path = await StoredImagePathAsync(added.Id);

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();

        _media.Files.Should().NotContainKey(path!);
        (await StoredImagePathAsync(added.Id)).Should().BeNull();
    }

    [Fact]
    public async Task A_removed_entry_keeps_its_picture_so_it_can_still_be_restored_until_the_member_withdraws_it()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var path = await StoredImagePathAsync(added.Id);

        var removed = await _activityEntryService.RejectAsync(added.Id, "Not sure.", null, "Moderator");

        _media.Files.Should().ContainKey(path!);
        _activityEntryService.ImageUrl(removed!).Should().Be("/media/" + path);

        (await controller.Withdraw(added.Id)).Should().BeOfType<NoContentResult>();
        _media.Files.Should().NotContainKey(path!);
        (await StoredImagePathAsync(added.Id)).Should().BeNull();
    }

    // ---- editing ----

    private static UpdateActivityEntryForm EditForm(IFormFile? image = null, bool removeImage = false, string url = Link, string title = "My post") =>
        new() { Kind = "blog", Title = title, Url = url, Date = "2026-10-01", Description = "About a thing.", Image = image, RemoveImage = removeImage };

    [Fact]
    public async Task An_edit_without_a_picture_keeps_the_one_the_entry_has()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var path = await StoredImagePathAsync(added.Id);

        var edited = Added(await controller.Update(added.Id, EditForm(title: "Renamed")));

        edited.ImageUrl.Should().Be(added.ImageUrl);
        (await StoredImagePathAsync(added.Id)).Should().Be(path);
        _media.Files.Should().ContainKey(path!);
    }

    [Fact]
    public async Task A_new_picture_replaces_the_old_one_which_is_deleted()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var oldPath = await StoredImagePathAsync(added.Id);

        var edited = Added(await controller.Update(added.Id, EditForm(Upload(Jpeg, "new.jpg"))));

        var newPath = await StoredImagePathAsync(added.Id);
        newPath.Should().NotBe(oldPath).And.EndWith(".jpg");
        edited.ImageUrl.Should().Be("/media/" + newPath);
        StoredPictures.Should().BeEquivalentTo(newPath);
    }

    [Fact]
    public async Task A_picture_can_be_added_to_an_entry_that_had_none()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form()));

        var edited = Added(await controller.Update(added.Id, EditForm(Upload(Png))));

        edited.ImageUrl.Should().NotBeNull();
        StoredPictures.Should().ContainSingle();
    }

    [Fact]
    public async Task Removing_the_picture_deletes_it_and_the_entry_stops_pointing_at_it()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));

        var edited = Added(await controller.Update(added.Id, EditForm(removeImage: true)));

        edited.ImageUrl.Should().BeNull();
        (await StoredImagePathAsync(added.Id)).Should().BeNull();
        StoredPictures.Should().BeEmpty();
    }

    [Fact]
    public async Task A_new_picture_wins_over_removing()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));

        Added(await controller.Update(added.Id, EditForm(Upload(Gif, "new.gif"), removeImage: true)));

        (await StoredImagePathAsync(added.Id)).Should().EndWith(".gif");
        StoredPictures.Should().ContainSingle();
    }

    [Fact]
    public async Task When_an_edit_is_refused_the_new_picture_is_deleted_and_the_old_one_kept()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        Added(await controller.Add(Form(url: "https://example.dev/other")));
        var oldPath = await StoredImagePathAsync(added.Id);

        // Another entry's link: refused after the new picture was stored.
        (await controller.Update(added.Id, EditForm(Upload(Jpeg), url: "https://example.dev/other"))).Should().BeOfType<ConflictObjectResult>();

        StoredPictures.Should().BeEquivalentTo(oldPath);
        (await StoredImagePathAsync(added.Id)).Should().Be(oldPath);
    }

    [Fact]
    public async Task Editing_a_removed_entry_stores_no_picture_and_keeps_its_own()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var oldPath = await StoredImagePathAsync(added.Id);
        await _activityEntryService.RejectAsync(added.Id, "No.", null, "Moderator");

        (await controller.Update(added.Id, EditForm(Upload(Jpeg), removeImage: true))).Should().BeOfType<ConflictObjectResult>();

        StoredPictures.Should().BeEquivalentTo(oldPath);
    }

    [Fact]
    public async Task A_bad_new_picture_is_a_400_and_the_entry_keeps_its_picture()
    {
        await CreateProfileAsync();
        var controller = CreateController();
        var added = Added(await controller.Add(Form(Upload(Png))));
        var oldPath = await StoredImagePathAsync(added.Id);

        var result = await controller.Update(added.Id, EditForm(Upload("not a picture"u8.ToArray(), "photo.jpg")));

        result.Should().BeOfType<BadRequestObjectResult>();
        StoredPictures.Should().BeEquivalentTo(oldPath);
    }

    [Fact]
    public async Task When_saving_an_edit_fails_the_new_picture_is_deleted_and_the_old_one_kept()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Upload(Png))));
        var oldPath = await StoredImagePathAsync(added.Id);
        var (newPath, _) = await _images.SaveAsync(new MemoryStream(Jpeg), Jpeg.Length);
        var profileId = (await _store.GetByMemberKeyAsync(_memberKey))!.Id;

        var editing = () => _activityEntryService.UpdateAsync(
            profileId,
            added.Id,
            new AddActivityEntryRequest("blog", "T", Link, new DateOnly(2026, 10, 1), null),
            ActivityEntryImageChange.Replace(newPath!),
            new CancellationToken(canceled: true));

        await editing.Should().ThrowAsync<OperationCanceledException>();
        _media.Files.Should().NotContainKey(newPath!);
        StoredPictures.Should().BeEquivalentTo(oldPath);
        (await StoredImagePathAsync(added.Id)).Should().Be(oldPath);
    }

    // ---- the timeline ----

    [Fact]
    public async Task An_entry_with_a_picture_brings_it_to_its_timeline_entry()
    {
        await CreateProfileAsync();
        var added = Added(await CreateController().Add(Form(Upload(Png))));
        var provider = new MemberProfileDataProvider(
            _store,
            new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>(), Mock.Of<Umbraco.Cms.Core.Media.IImageUrlGenerator>()),
            TestImageProxy.Create());

        var enriched = await CreateActivityService().EnrichAsync((await provider.GetProfileAsync(Handle, includeHidden: true))!);

        enriched.ActivityImages.Should().ContainKey(ProfileActivityEntry.ActivityId(added.Id)).WhoseValue.Should().Be(added.ImageUrl);
        ProfileTimeline.Entries(enriched).Should().ContainSingle(e => e.ActivityId == ProfileActivityEntry.ActivityId(added.Id))
            .Which.OwnImageUrl.Should().Be(added.ImageUrl);
    }

    [Fact]
    public void Only_entries_with_a_picture_are_given_one()
    {
        var withPicture = Record(1, "profile-activity/a.png");
        var without = Record(2, null);

        var profile = ProfileActivityService.AddVerifiedActivity(
            EmptyProfile(), [withPicture, without], e => e.ImagePath == null ? null : "/media/" + e.ImagePath);

        profile.ActivityImages.Should().Equal(new Dictionary<string, string> { [ProfileActivityEntry.ActivityId(1)] = "/media/profile-activity/a.png" });
        ProfileTimeline.Entries(profile).Single(e => e.ActivityId == ProfileActivityEntry.ActivityId(2)).OwnImageUrl.Should().BeNull();
    }

    private static ActivityEntryRecord Record(int id, string? imagePath) =>
        new(
            id,
            Handle,
            "The Octocat",
            "blog",
            $"Post {id}",
            $"https://example.dev/{id}",
            new DateOnly(2026, 10, id),
            null,
            Web.Features.Profiles.Data.Entities.ActivityEntryState.Verified,
            null,
            "Admin",
            DateTime.UtcNow,
            DateTime.UtcNow,
            imagePath);

    private static CommunityProfile EmptyProfile() =>
        new(
            Handle,
            null,
            new ProfileIdentity("The Octocat", null, null, null, null, null, false, [], Handle, null, [], false),
            [], [], [], [], [], [], [], []);
}

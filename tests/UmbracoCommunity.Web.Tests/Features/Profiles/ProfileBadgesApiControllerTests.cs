using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Controllers;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Features.Profiles.ForumBadges;
using UmbracoCommunity.Web.Features.Profiles.Models;
using UmbracoCommunity.Web.Tests.Features.Feeds.Calendar;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.Profiles;

/// <summary>
/// Runs the badge choices end to end: read from a stubbed forum, saved through the API into the real store, then read
/// back through the profile provider as the profile hero shows them.
/// </summary>
public class ProfileBadgesApiControllerTests : IDisposable
{
    private const string Handle = "octocat";

    // The member's forum username differs from their GitHub handle, as it can in real life.
    private const string ForumUser = "octo-forum";
    private const string ForumLink = "https://Forum.Umbraco.com/u/octo-forum/summary?tab=badges#top";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly MemberProfileStore _store;
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly List<Uri> _requests = [];

    // What the forum answers: the made-up badge list unless a test says otherwise.
    private Func<HttpResponseMessage> _forum = () => new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(ForumBadgeClientTests.UserBadgesJson),
    };

    public ProfileBadgesApiControllerTests()
    {
        _store = new MemberProfileStore(_database);
    }

    public void Dispose() => _database.Dispose();

    private async Task CreateProfileAsync(Guid memberKey, string handle, bool completeOnboarding = true, string? forumLink = ForumLink)
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        await _store.GetOrStartOnboardingAsync(memberKey, handle, handle);
        if (completeOnboarding)
        {
            await _store.CompleteOnboardingAsync(memberKey);
        }

        if (forumLink != null)
        {
            await _store.AddFeedAsync(memberKey, "Forum", forumLink);
        }
    }

    // A fresh client (and so a fresh cache) per call, so a test can change what the forum answers.
    private ForumBadgeService CreateBadgeService() =>
        new(ForumBadgeClientTests.CreateClient(new StubHandler(request =>
        {
            _requests.Add(request.RequestUri!);
            return _forum();
        })), TestImageProxy.Create());

    private ProfileBadgesApiController CreateController(bool signedIn = true)
    {
        var member = signedIn ? new MemberIdentityUser { Key = _memberKey, UserName = Handle } : null;
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.GetCurrentMemberAsync()).ReturnsAsync(member);
        return new ProfileBadgesApiController(memberManager.Object, _store, CreateBadgeService());
    }

    private MemberProfileDataProvider CreateProvider() => new(
        _store,
        new ProfileAvatarUrlResolver(Mock.Of<IPublishedContentQuery>(), Mock.Of<IPublishedUrlProvider>(), Mock.Of<Umbraco.Cms.Core.Media.IImageUrlGenerator>()),
        TestImageProxy.Create(),
        CreateBadgeService());

    private static T Body<T>(IActionResult result) => result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeOfType<T>().Which;

    private static string? Error(IActionResult result, int status)
    {
        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Which;
        objectResult.StatusCode.Should().Be(status);
        return objectResult.Value?.GetType().GetProperty("error")?.GetValue(objectResult.Value) as string;
    }

    [Fact]
    public async Task Lists_the_forum_badges_of_the_user_in_the_members_forum_link()
    {
        await CreateProfileAsync(_memberKey, Handle);

        var body = Body<ProfileBadgesResponse>(await CreateController().Get(default));

        _requests.Should().Equal(new Uri("https://forum.umbraco.com/user-badges/octo-forum.json"));
        body.Status.Should().Be("ok");
        body.ForumUsername.Should().Be(ForumUser);
        body.ForumProfileUrl.Should().Be("https://forum.umbraco.com/u/octo-forum");
        body.MaxShown.Should().Be(ForumBadgeService.MaxShown);
        body.Badges.Select(b => b.Name).Should().Equal("Umbraco MVP", "Great Topic", "Member", "Nice Reply");
        body.Badges.Should().OnlyContain(b => !b.Shown);
        body.ShownIds.Should().BeEmpty();

        var mvp = body.Badges[0];
        mvp.IconUrl.Should().StartWith("/api/feed-submission/image-proxy?token=", "pictures only ever load from this site");
        mvp.Url.Should().Be("https://forum.umbraco.com/badges/107/umbraco-mvp");
        body.Badges.Single(b => b.Name == "Member").IconUrl.Should().BeNull();
    }

    [Fact]
    public async Task Saves_the_chosen_badges_dropping_ones_the_member_does_not_hold()
    {
        await CreateProfileAsync(_memberKey, Handle);

        var body = Body<ProfileBadgesResponse>(await CreateController().Update(new UpdateProfileBadgesRequest([107, 999, 2, 107, 9]), default));

        body.ShownIds.Should().Equal(107, 2);
        body.Badges.Where(b => b.Shown).Select(b => b.Id).Should().Equal(107, 2);
        (await _store.GetByMemberKeyAsync(_memberKey))!.ShownForumBadgeIds.Should().BeEquivalentTo([107, 2]);

        var again = Body<ProfileBadgesResponse>(await CreateController().Get(default));
        again.ShownIds.Should().Equal(107, 2);
    }

    [Fact]
    public async Task An_empty_choice_shows_none()
    {
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [107]);

        var body = Body<ProfileBadgesResponse>(await CreateController().Update(new UpdateProfileBadgesRequest(null), default));

        body.ShownIds.Should().BeEmpty();
        (await _store.GetByMemberKeyAsync(_memberKey))!.ShownForumBadgeIds.Should().BeEmpty();
    }

    [Fact]
    public async Task More_badges_than_the_cap_are_refused()
    {
        // Ten badges the member holds.
        var ids = Enumerable.Range(1, ForumBadgeService.MaxShown + 2).ToList();
        var json = $$"""
            {
              "badges": [ {{string.Join(",", ids.Select(i => $$"""{ "id": {{i}}, "name": "Badge {{i}}", "badge_type_id": 3 }"""))}} ],
              "badge_types": [ { "id": 3, "name": "Bronze", "sort_order": 7 } ],
              "user_badges": [ {{string.Join(",", ids.Select(i => $$"""{ "badge_id": {{i}} }"""))}} ]
            }
            """;
        _forum = () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        await CreateProfileAsync(_memberKey, Handle);

        var result = await CreateController().Update(new UpdateProfileBadgesRequest(ids), default);

        Error(result, StatusCodes.Status400BadRequest).Should().Contain($"up to {ForumBadgeService.MaxShown} badges");
        (await _store.GetByMemberKeyAsync(_memberKey))!.ShownForumBadgeIds.Should().BeEmpty();

        var allowed = await CreateController().Update(new UpdateProfileBadgesRequest(ids.Take(ForumBadgeService.MaxShown).ToList()), default);
        Body<ProfileBadgesResponse>(allowed).ShownIds.Should().HaveCount(ForumBadgeService.MaxShown);
    }

    [Fact]
    public async Task Only_changes_the_signed_in_members_own_choice()
    {
        var otherKey = Guid.NewGuid();
        await CreateProfileAsync(otherKey, "someone-else");
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(otherKey, [5]);

        await CreateController().Update(new UpdateProfileBadgesRequest([107]), default);

        (await _store.GetByMemberKeyAsync(otherKey))!.ShownForumBadgeIds.Should().Equal(5);
        (await _store.GetByMemberKeyAsync(_memberKey))!.ShownForumBadgeIds.Should().Equal(107);
        _requests.Should().OnlyContain(u => u.AbsolutePath == "/user-badges/octo-forum.json");
    }

    [Fact]
    public async Task A_member_without_a_forum_link_is_told_to_add_one_and_the_GitHub_handle_is_never_tried()
    {
        await CreateProfileAsync(_memberKey, Handle, forumLink: null);
        await _store.AddFeedAsync(_memberKey, "Blog", "https://octo.example");

        var body = Body<ProfileBadgesResponse>(await CreateController().Get(default));
        body.Status.Should().Be("noForumLink");
        body.ForumUsername.Should().BeNull();
        body.ForumProfileUrl.Should().BeNull();
        body.Badges.Should().BeEmpty();

        var result = await CreateController().Update(new UpdateProfileBadgesRequest([107]), default);
        Error(result, StatusCodes.Status400BadRequest).Should().Contain("Feeds & links");
        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_changed_forum_link_only_shows_chosen_badges_the_new_user_holds()
    {
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [107, 2]);
        var link = (await _store.GetActiveFeedsAsync(_memberKey)).Single();

        // The new account only holds badge 2.
        await _store.UpdateFeedAsync(_memberKey, link.Id, "Forum", "https://forum.umbraco.com/u/someone-new", null);
        _forum = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
                { "badges": [ { "id": 2, "name": "Member", "badge_type_id": 3 } ],
                  "badge_types": [ { "id": 3, "name": "Bronze", "sort_order": 7 } ],
                  "user_badges": [ { "badge_id": 2 } ] }
                """),
        };

        var body = Body<ProfileBadgesResponse>(await CreateController().Get(default));

        _requests.Should().Equal(new Uri("https://forum.umbraco.com/user-badges/someone-new.json"));
        body.ShownIds.Should().Equal(2);
        await _store.SetProfileVisibleAsync(_memberKey, true);
        (await CreateProvider().GetProfileAsync(Handle))!.Badges.Select(b => b.Name).Should().Equal("Member");
    }

    [Fact]
    public async Task A_member_with_no_forum_account_gets_not_found_and_cannot_save()
    {
        _forum = () => new HttpResponseMessage(HttpStatusCode.NotFound);
        await CreateProfileAsync(_memberKey, Handle);

        var body = Body<ProfileBadgesResponse>(await CreateController().Get(default));
        body.Status.Should().Be("notFound");
        body.Badges.Should().BeEmpty();

        var result = await CreateController().Update(new UpdateProfileBadgesRequest([107]), default);
        Error(result, StatusCodes.Status400BadRequest).Should().Contain("couldn't find that user on the forum");
    }

    [Fact]
    public async Task An_unreachable_forum_is_reported_and_nothing_is_saved()
    {
        _forum = () => throw new HttpRequestException("down");
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [107]);

        Body<ProfileBadgesResponse>(await CreateController().Get(default)).Status.Should().Be("unavailable");

        var result = await CreateController().Update(new UpdateProfileBadgesRequest([]), default);
        Error(result, StatusCodes.Status503ServiceUnavailable).Should().Contain("couldn't reach the forum");
        (await _store.GetByMemberKeyAsync(_memberKey))!.ShownForumBadgeIds.Should().Equal(107);
    }

    [Fact]
    public async Task Signed_out_is_unauthorized()
    {
        await CreateProfileAsync(_memberKey, Handle);

        (await CreateController(signedIn: false).Get(default)).Should().BeOfType<UnauthorizedResult>();
        (await CreateController(signedIn: false).Update(new UpdateProfileBadgesRequest([107]), default)).Should().BeOfType<UnauthorizedResult>();
        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task A_member_who_has_not_finished_onboarding_is_refused()
    {
        await CreateProfileAsync(_memberKey, Handle, completeOnboarding: false);

        Error(await CreateController().Get(default), StatusCodes.Status400BadRequest).Should().Be("Finish setting up your profile first.");
        Error(await CreateController().Update(new UpdateProfileBadgesRequest([107]), default), StatusCodes.Status400BadRequest)
            .Should().Be("Finish setting up your profile first.");
    }

    [Fact]
    public async Task The_profile_shows_the_chosen_badges_the_member_still_holds()
    {
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [2, 107, 12345]);
        await _store.SetProfileVisibleAsync(_memberKey, true);

        var provider = CreateProvider();

        var profile = await provider.GetProfileAsync(Handle);

        profile!.Badges.Select(b => (b.Name, b.BadgeType)).Should().Equal(("Umbraco MVP", "Gold"), ("Member", "Bronze"));
        profile.Badges[0].IconUrl.Should().StartWith("/api/feed-submission/image-proxy?token=");
        profile.Badges[1].IconUrl.Should().BeNull();
        profile.Badges.Select(b => b.Url).Should().Equal("https://forum.umbraco.com/badges/107/umbraco-mvp", "https://forum.umbraco.com/badges/2/member");
    }

    [Fact]
    public async Task The_profile_leaves_the_badges_out_when_the_forum_cannot_be_reached()
    {
        _forum = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [107]);
        await _store.SetProfileVisibleAsync(_memberKey, true);

        var provider = CreateProvider();

        var profile = await provider.GetProfileAsync(Handle);

        profile.Should().NotBeNull();
        profile!.Badges.Should().BeEmpty();
    }

    [Fact]
    public async Task The_profile_shows_no_badges_once_the_forum_link_is_removed()
    {
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetShownForumBadgesAsync(_memberKey, [107]);
        await _store.SetProfileVisibleAsync(_memberKey, true);
        await _store.RemoveFeedAsync(_memberKey, (await _store.GetActiveFeedsAsync(_memberKey)).Single().Id, null);

        (await CreateProvider().GetProfileAsync(Handle))!.Badges.Should().BeEmpty();
        _requests.Should().BeEmpty();
    }

    [Fact]
    public async Task The_profile_does_not_ask_the_forum_when_no_badges_are_chosen()
    {
        await CreateProfileAsync(_memberKey, Handle);
        await _store.SetProfileVisibleAsync(_memberKey, true);

        (await CreateProvider().GetProfileAsync(Handle))!.Badges.Should().BeEmpty();
        _requests.Should().BeEmpty();
    }
}

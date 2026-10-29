using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Security;
using UmbracoCommunity.Web.Features.Profiles;
using UmbracoCommunity.Web.Features.Profiles.Data;
using UmbracoCommunity.Web.Models.ViewModels.Components;
using UmbracoCommunity.Web.Tests.Features.Profiles;
using UmbracoCommunity.Web.ViewModelBuilders.Components;
using Xunit;

namespace UmbracoCommunity.Web.Tests.ViewModelBuilders.Components;

public class MenuViewModelBuilderTests : IDisposable
{
    private const string Handle = "octocat";
    private const string GitHubAvatarUrl = "https://github.com/octocat.png";
    private const string UploadedAvatarUrl = "/media/abc123/octocat.png";

    private readonly MemberProfilesTestDatabase _database = new();
    private readonly Guid _memberKey = Guid.NewGuid();
    private readonly Guid _avatarMediaKey = Guid.NewGuid();

    public void Dispose() => _database.Dispose();

    private async Task<MemberProfileStore> CreateStoreAsync()
    {
        await using (var context = _database.CreateDbContext())
        {
            await context.Database.MigrateAsync();
        }

        return new MemberProfileStore(_database);
    }

    private static Mock<IMemberManager> SignedInMemberManager(Guid memberKey)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(true);
        memberManager
            .Setup(m => m.GetCurrentMemberAsync())
            .ReturnsAsync(new MemberIdentityUser { Key = memberKey, UserName = Handle });
        return memberManager;
    }

    private MenuViewModelBuilder CreateBuilder(IMemberManager memberManager, MemberProfileStore store)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, Handle)], "Test")),
        };

        // An uploaded avatar resolves to its media URL; nothing else is published.
        var mediaType = new Mock<IPublishedContentType>();
        mediaType.SetupGet(t => t.ItemType).Returns(PublishedItemType.Media);
        var media = new Mock<IPublishedContent>();
        media.SetupGet(m => m.ContentType).Returns(mediaType.Object);
        var contentQuery = new Mock<IPublishedContentQuery>();
        contentQuery.Setup(q => q.Media(_avatarMediaKey)).Returns(media.Object);
        var urlProvider = new Mock<IPublishedUrlProvider>();
        urlProvider
            .Setup(p => p.GetMediaUrl(media.Object, It.IsAny<UrlMode>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Uri?>()))
            .Returns(UploadedAvatarUrl);

        return new MenuViewModelBuilder(
            contentQuery.Object,
            urlProvider.Object,
            AppCaches.NoCache,
            new HttpContextAccessor { HttpContext = httpContext },
            memberManager,
            store,
            new ProfileAvatarUrlResolver(contentQuery.Object, urlProvider.Object),
            NullLogger<MenuViewModelBuilder>.Instance);
    }

    [Fact]
    public async Task A_member_with_an_uploaded_avatar_gets_the_uploaded_avatar()
    {
        var store = await CreateStoreAsync();
        await store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        await store.UpdateAvatarAsync(_memberKey, _avatarMediaKey);
        var builder = CreateBuilder(SignedInMemberManager(_memberKey).Object, store);
        var viewModel = new MenuViewModel();

        await builder.PopulateMemberAsync(viewModel, signInEnabled: true);

        viewModel.IsSignedIn.Should().BeTrue();
        viewModel.MemberAvatarUrl.Should().Be(UploadedAvatarUrl);
    }

    [Fact]
    public async Task A_member_without_an_uploaded_avatar_gets_the_GitHub_avatar()
    {
        var store = await CreateStoreAsync();
        await store.GetOrStartOnboardingAsync(_memberKey, Handle, "The Octocat");
        var builder = CreateBuilder(SignedInMemberManager(_memberKey).Object, store);
        var viewModel = new MenuViewModel();

        await builder.PopulateMemberAsync(viewModel, signInEnabled: true);

        viewModel.MemberAvatarUrl.Should().Be(GitHubAvatarUrl);
    }

    [Fact]
    public async Task A_member_without_a_profile_gets_the_GitHub_avatar()
    {
        var store = await CreateStoreAsync();
        var builder = CreateBuilder(SignedInMemberManager(_memberKey).Object, store);
        var viewModel = new MenuViewModel();

        await builder.PopulateMemberAsync(viewModel, signInEnabled: true);

        viewModel.MemberAvatarUrl.Should().Be(GitHubAvatarUrl);
    }

    [Fact]
    public async Task A_failing_lookup_falls_back_to_the_GitHub_avatar()
    {
        var failingFactory = new Mock<IDbContextFactory<MemberProfilesDbContext>>();
        failingFactory
            .Setup(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database unavailable"));
        var builder = CreateBuilder(SignedInMemberManager(_memberKey).Object, new MemberProfileStore(failingFactory.Object));
        var viewModel = new MenuViewModel();

        await builder.PopulateMemberAsync(viewModel, signInEnabled: true);

        viewModel.IsSignedIn.Should().BeTrue();
        viewModel.MemberAvatarUrl.Should().Be(GitHubAvatarUrl);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task No_lookup_happens_unless_sign_in_is_enabled_and_a_member_is_signed_in(bool signInEnabled, bool isLoggedIn)
    {
        var memberManager = new Mock<IMemberManager>();
        memberManager.Setup(m => m.IsLoggedIn()).Returns(isLoggedIn);
        var factory = new Mock<IDbContextFactory<MemberProfilesDbContext>>();
        var builder = CreateBuilder(memberManager.Object, new MemberProfileStore(factory.Object));
        var viewModel = new MenuViewModel();

        await builder.PopulateMemberAsync(viewModel, signInEnabled);

        viewModel.IsSignedIn.Should().BeFalse();
        viewModel.MemberAvatarUrl.Should().BeNull();
        memberManager.Verify(m => m.GetCurrentMemberAsync(), Times.Never);
        factory.Verify(f => f.CreateDbContextAsync(It.IsAny<CancellationToken>()), Times.Never);
        factory.Verify(f => f.CreateDbContext(), Times.Never);
    }
}

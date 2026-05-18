using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
using Umbraco.Community.NotFoundTracker.Services;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class NotFoundUserScopeServiceTests
{
    [Fact]
    public void Full_access_when_start_nodes_include_root()
    {
        var user = MockUser(startNodes: new[] { -1 });
        var sut = Build(user, domains: Array.Empty<IDomain>());

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeTrue();
        scope.AccessibleHostnames.Should().BeEmpty();
        scope.CanAccessHostname("any-host.example").Should().BeTrue();
    }

    [Fact]
    public void No_user_returns_empty_scope_without_full_access()
    {
        var sut = Build(currentUser: null, domains: Array.Empty<IDomain>());

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeFalse();
        scope.AccessibleHostnames.Should().BeEmpty();
        scope.CanAccessHostname("any").Should().BeFalse();
    }

    [Fact]
    public void Single_start_node_collects_its_domains()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[]
        {
            MockDomain("site-a.example", contentId: 100),
            MockDomain("site-b.example", contentId: 200),  // not accessible
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.HasFullAccess.Should().BeFalse();
        scope.AccessibleHostnames.Should().BeEquivalentTo(["site-a.example"]);
        scope.CanAccessHostname("site-a.example").Should().BeTrue();
        scope.CanAccessHostname("site-b.example").Should().BeFalse();
    }

    [Fact]
    public void Hostnames_are_lowercased_in_the_scope()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[] { MockDomain("Site-A.Example", contentId: 100) };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().Contain("site-a.example");
        scope.CanAccessHostname("site-a.example").Should().BeTrue();
    }

    [Fact]
    public void Multiple_start_nodes_union_their_hostnames()
    {
        var user = MockUser(startNodes: new[] { 100, 200 });
        var domains = new[]
        {
            MockDomain("a.example", contentId: 100),
            MockDomain("b.example", contentId: 200),
            MockDomain("c.example", contentId: 300),  // not in start nodes
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().BeEquivalentTo(["a.example", "b.example"]);
    }

    [Fact]
    public void Wildcard_domain_without_name_is_ignored()
    {
        var user = MockUser(startNodes: new[] { 100 });
        var domains = new[]
        {
            MockDomain(null, contentId: 100),               // wildcard — no hostname to expose
            MockDomain("a.example", contentId: 100),
        };
        var sut = Build(user, domains);

        var scope = sut.GetCurrentScope();

        scope.AccessibleHostnames.Should().BeEquivalentTo(["a.example"]);
    }

    private static IUser MockUser(int[] startNodes)
    {
        var user = new Mock<IUser>();
        user.Setup(u => u.StartContentIds).Returns(startNodes);
        return user.Object;
    }

    private static IDomain MockDomain(string? name, int contentId)
    {
        var d = new Mock<IDomain>();
        d.Setup(x => x.DomainName).Returns(name);
        d.Setup(x => x.RootContentId).Returns(contentId);
        return d.Object;
    }

    private static NotFoundUserScopeService Build(IUser? currentUser, IEnumerable<IDomain> domains)
    {
        var security = new Mock<IBackOfficeSecurityAccessor>();
        var backOfficeSecurity = new Mock<IBackOfficeSecurity>();
        backOfficeSecurity.Setup(b => b.CurrentUser).Returns(currentUser);
        security.Setup(s => s.BackOfficeSecurity).Returns(backOfficeSecurity.Object);

        var domainService = new Mock<IDomainService>();
        domainService.Setup(d => d.GetAll(It.IsAny<bool>())).Returns(domains);

        return new NotFoundUserScopeService(
            security.Object,
            domainService.Object,
            NullLogger<NotFoundUserScopeService>.Instance);
    }
}

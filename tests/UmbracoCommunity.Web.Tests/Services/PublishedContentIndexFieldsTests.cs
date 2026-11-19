using FluentAssertions;
using UmbracoCommunity.Web.Services;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Services;

public class PublishedContentIndexFieldsTests
{
    [Fact]
    public void Field_NamesPropertyFieldsByKind()
    {
        PublishedContentIndexFields.Field("metaTitle").Should().Be("Field_metaTitle_texts");
        PublishedContentIndexFields.Field("contentBlocks", PublishedContentIndexFields.TextsR2).Should().Be("Field_contentBlocks_textsr2");
    }

    [Fact]
    public void SystemFields_MatchTheIndexSchema()
    {
        PublishedContentIndexFields.Name.Should().Be("Field_Umb_Name_textsr1");
        PublishedContentIndexFields.Key.Should().Be("Field_Umb_Id_keywords");
    }

    [Fact]
    public void IsPublic_TrueForTheEmptyAccessId()
    {
        PublishedContentIndexFields.IsPublic(["00000000-0000-0000-0000-000000000000"]).Should().BeTrue();
    }

    [Fact]
    public void IsPublic_FalseForMemberProtectedOrMissingProtection()
    {
        PublishedContentIndexFields.IsPublic(["5a4d3443-df6f-4670-87f4-e5eb2424d3b4", "0e2c4b3d-11aa-4c7e-9a43-7f0c9d1b2a33"]).Should().BeFalse();
        PublishedContentIndexFields.IsPublic([]).Should().BeFalse();
    }

    [Fact]
    public void FirstProse_SkipsFileNamesHostsAndLabels()
    {
        PublishedContentIndexFields.FirstProse(
                ["community_findyourpeople_16-9.mp4", "&nbsp;", "Discord", "  ", "Get involved in the community", "Later text here"])
            .Should().Be("Get involved in the community");
    }

    [Fact]
    public void FirstProse_NullWhenNothingReadsLikeProse()
    {
        PublishedContentIndexFields.FirstProse(["forum.umbraco.com", "Mastodon"]).Should().BeNull();
    }
}

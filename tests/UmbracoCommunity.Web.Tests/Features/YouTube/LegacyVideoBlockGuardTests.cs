using FluentAssertions;
using UmbracoCommunity.Web.Features.YouTube;
using Xunit;

namespace UmbracoCommunity.Web.Tests.Features.YouTube;

public class LegacyVideoBlockGuardTests
{
    private static readonly Guid LegacyType = Guid.Parse("a22baea7-24fa-4cc5-bab7-8f68e2d16028");
    private const string OtherType = "b1a7fb4d-21d3-4820-872a-79f99d8b0a47";

    private static string Block(string typeKey, string key) => $$"""{"contentTypeKey":"{{typeKey}}","key":"{{key}}","values":[]}""";

    [Fact]
    public void Finds_legacy_blocks_in_a_block_grid()
    {
        var grid = $$"""{"layout":{},"contentData":[{{Block(LegacyType.ToString(), "k1")}},{{Block(OtherType, "k2")}}],"settingsData":[]}""";

        LegacyVideoBlockGuard.BlockKeys([grid], LegacyType).Should().BeEquivalentTo(["k1"]);
    }

    [Fact]
    public void Finds_legacy_blocks_nested_as_a_serialised_string()
    {
        var inner = $$"""{"contentData":[{{Block(LegacyType.ToString(), "nested")}}]}""";
        var outer = $$"""{"contentData":[{"contentTypeKey":"{{OtherType}}","key":"outer","values":[{"alias":"items","value":{{System.Text.Json.JsonSerializer.Serialize(inner)}}}]}]}""";

        LegacyVideoBlockGuard.BlockKeys([outer], LegacyType).Should().BeEquivalentTo(["nested"]);
    }

    [Fact]
    public void Ignores_values_without_legacy_blocks()
    {
        LegacyVideoBlockGuard.BlockKeys(["plain text", $$"""{"contentData":[{{Block(OtherType, "k2")}}]}"""], LegacyType)
            .Should().BeEmpty();
    }

    [Fact]
    public void Survives_broken_json()
    {
        LegacyVideoBlockGuard.BlockKeys([$"{{\"contentTypeKey\":\"{LegacyType}\""], LegacyType).Should().BeEmpty();
    }
}

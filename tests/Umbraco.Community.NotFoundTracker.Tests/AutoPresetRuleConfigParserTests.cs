using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Infrastructure;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Tests;

public class AutoPresetRuleConfigParserTests
{
    [Theory]
    [InlineData("Exact", IgnoreMatchType.Exact)]
    [InlineData("exact", IgnoreMatchType.Exact)]
    [InlineData("EXACT", IgnoreMatchType.Exact)]
    [InlineData("PathPrefix", IgnoreMatchType.PathPrefix)]
    [InlineData("pathprefix", IgnoreMatchType.PathPrefix)]
    public void Parses_valid_match_types(string input, IgnoreMatchType expected)
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = input };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.MatchType.Should().Be(expected);
        parsed.Path.Should().Be("/foo");
    }

    [Fact]
    public void Throws_on_invalid_match_type_with_actionable_message()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = "Regex" };

        var act = () => AutoPresetRuleConfigParser.Parse(cfg);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Regex*Exact*PathPrefix*");
    }

    [Fact]
    public void Throws_on_empty_path()
    {
        var cfg = new AutoPresetRuleConfig { Path = "", MatchType = "Exact" };

        var act = () => AutoPresetRuleConfigParser.Parse(cfg);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Path*");
    }

    [Fact]
    public void Lowercases_path_and_normalizes_hostname()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/FOO", MatchType = "Exact", Hostname = "Example.COM" };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.Path.Should().Be("/foo");
        parsed.Hostname.Should().Be("example.com");
    }

    [Fact]
    public void Preserves_null_hostname()
    {
        var cfg = new AutoPresetRuleConfig { Path = "/foo", MatchType = "Exact", Hostname = null };

        var parsed = AutoPresetRuleConfigParser.Parse(cfg);

        parsed.Hostname.Should().BeNull();
    }
}

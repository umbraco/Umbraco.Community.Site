using Umbraco.Community.NotFoundTracker.Configuration;
using Umbraco.Community.NotFoundTracker.Matching;
using Umbraco.Community.NotFoundTracker.Models.Entities;

namespace Umbraco.Community.NotFoundTracker.Infrastructure;

/// <summary>
/// Parses an <see cref="AutoPresetRuleConfig"/> (from appsettings.json) into a
/// <see cref="ParsedAutoPresetRule"/> with the typed enum + normalized path/hostname.
/// Bound as <c>string</c> in the options class rather than the enum directly so
/// invalid values produce a clean error from this parser instead of a generic
/// configuration binding failure.
/// </summary>
public static class AutoPresetRuleConfigParser
{
    public static ParsedAutoPresetRule Parse(AutoPresetRuleConfig cfg)
    {
        if (string.IsNullOrEmpty(cfg.Path))
        {
            throw new InvalidOperationException(
                "AutoPresetRuleConfig.Path is required and cannot be empty.");
        }

        if (!Enum.TryParse<IgnoreMatchType>(cfg.MatchType, ignoreCase: true, out var matchType))
        {
            throw new InvalidOperationException(
                $"Invalid AutoPresetRuleConfig.MatchType '{cfg.MatchType}'. Expected one of: Exact, PathPrefix.");
        }

        return new ParsedAutoPresetRule(
            Path: UrlNormalizer.NormalizePath(cfg.Path),
            MatchType: matchType,
            Hostname: string.IsNullOrEmpty(cfg.Hostname) ? null : UrlNormalizer.NormalizeHostname(cfg.Hostname),
            Note: cfg.Note);
    }
}

public sealed record ParsedAutoPresetRule(
    string Path,
    IgnoreMatchType MatchType,
    string? Hostname,
    string? Note);

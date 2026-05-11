using System.Text.RegularExpressions;

namespace UmbracoCommunity.Web.Helpers;

/// <summary>
/// Best-effort country-flag inference for calendar event entries that don't
/// carry a structured country code. Recognises:
///   1. A trailing ISO 3166-1 alpha-2 code in the location (e.g. "…, AU")
///   2. A country name, adjective, ISO code, or major unambiguous city name
///      anywhere in the location, organizer, or title text
/// Returns null when nothing matches — partial coverage by design.
/// </summary>
public static class CountryFlagHelper
{
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // Country names, adjectives, ISO codes
        ["India"] = "IN", ["Indian"] = "IN",
        ["US"] = "US", ["USA"] = "US", ["American"] = "US",
        ["UK"] = "GB", ["British"] = "GB", ["Britain"] = "GB", ["England"] = "GB",
        ["Australia"] = "AU", ["Australian"] = "AU",
        ["Belgium"] = "BE", ["Belgian"] = "BE",
        ["Netherlands"] = "NL", ["Dutch"] = "NL",
        ["Germany"] = "DE", ["German"] = "DE",
        ["Denmark"] = "DK", ["Danish"] = "DK",
        ["Sweden"] = "SE", ["Swedish"] = "SE",
        ["Norway"] = "NO", ["Norwegian"] = "NO",
        ["Finland"] = "FI", ["Finnish"] = "FI",
        ["Italy"] = "IT", ["Italian"] = "IT",
        ["France"] = "FR", ["French"] = "FR",
        ["Spain"] = "ES", ["Spanish"] = "ES",
        ["Switzerland"] = "CH", ["Swiss"] = "CH",
        ["Austria"] = "AT", ["Austrian"] = "AT",
        ["Poland"] = "PL", ["Polish"] = "PL",
        ["Ireland"] = "IE", ["Irish"] = "IE",
        ["Canada"] = "CA", ["Canadian"] = "CA",
        ["Japan"] = "JP", ["Japanese"] = "JP",
        ["Brazil"] = "BR", ["Brazilian"] = "BR",
        ["Mexico"] = "MX", ["Mexican"] = "MX",
        ["Portugal"] = "PT", ["Portuguese"] = "PT",
        ["Hungary"] = "HU", ["Hungarian"] = "HU",
        ["Romania"] = "RO", ["Romanian"] = "RO",
        ["Czech"] = "CZ", ["Slovakia"] = "SK", ["Slovak"] = "SK",
        ["Greece"] = "GR", ["Greek"] = "GR",
        ["Singapore"] = "SG",

        // UK regions/counties/devolved nations
        ["Wales"] = "GB", ["Welsh"] = "GB",
        ["Scotland"] = "GB", ["Scottish"] = "GB",
        ["Kent"] = "GB", ["Sussex"] = "GB", ["Yorkshire"] = "GB", ["Cornwall"] = "GB", ["Devon"] = "GB",

        // Major unambiguous cities
        ["London"] = "GB", ["Manchester"] = "GB", ["Edinburgh"] = "GB", ["Glasgow"] = "GB", ["Bristol"] = "GB", ["Liverpool"] = "GB", ["Birmingham"] = "GB", ["Oxford"] = "GB", ["Brighton"] = "GB", ["Newcastle"] = "GB", ["Cardiff"] = "GB", ["Belfast"] = "GB", ["Leeds"] = "GB",
        ["Sydney"] = "AU", ["Melbourne"] = "AU", ["Brisbane"] = "AU", ["Perth"] = "AU", ["Adelaide"] = "AU", ["Canberra"] = "AU",
        ["Copenhagen"] = "DK", ["Aarhus"] = "DK", ["Odense"] = "DK",
        ["Amsterdam"] = "NL", ["Rotterdam"] = "NL", ["Utrecht"] = "NL", ["Eindhoven"] = "NL",
        ["Berlin"] = "DE", ["Munich"] = "DE", ["Hamburg"] = "DE", ["Frankfurt"] = "DE", ["Cologne"] = "DE",
        ["Brussels"] = "BE", ["Antwerp"] = "BE", ["Ghent"] = "BE",
        ["Paris"] = "FR", ["Lyon"] = "FR", ["Marseille"] = "FR",
        ["Rome"] = "IT", ["Milan"] = "IT", ["Florence"] = "IT", ["Naples"] = "IT", ["Turin"] = "IT",
        ["Madrid"] = "ES", ["Barcelona"] = "ES", ["Valencia"] = "ES", ["Seville"] = "ES",
        ["Stockholm"] = "SE", ["Gothenburg"] = "SE",
        ["Oslo"] = "NO", ["Bergen"] = "NO",
        ["Helsinki"] = "FI",
        ["Warsaw"] = "PL", ["Krakow"] = "PL",
        ["Zurich"] = "CH", ["Geneva"] = "CH", ["Basel"] = "CH", ["Bern"] = "CH",
        ["Vienna"] = "AT", ["Salzburg"] = "AT",
        ["Dublin"] = "IE", ["Cork"] = "IE",
        ["Toronto"] = "CA", ["Montreal"] = "CA", ["Vancouver"] = "CA", ["Ottawa"] = "CA",
        ["Chicago"] = "US", ["Boston"] = "US", ["Seattle"] = "US", ["Atlanta"] = "US", ["Denver"] = "US", ["Austin"] = "US", ["Houston"] = "US", ["Dallas"] = "US", ["Phoenix"] = "US", ["Philadelphia"] = "US", ["Miami"] = "US", ["Detroit"] = "US", ["Minneapolis"] = "US",
        ["Mumbai"] = "IN", ["Delhi"] = "IN", ["Bangalore"] = "IN", ["Bengaluru"] = "IN", ["Hyderabad"] = "IN", ["Chennai"] = "IN", ["Pune"] = "IN", ["Kolkata"] = "IN",
        ["Tokyo"] = "JP", ["Osaka"] = "JP", ["Kyoto"] = "JP",
        ["Prague"] = "CZ",
        ["Budapest"] = "HU",
        ["Lisbon"] = "PT", ["Porto"] = "PT",
        ["Auckland"] = "NZ", ["Wellington"] = "NZ", ["Christchurch"] = "NZ",
    };

    private static readonly Regex WordPattern = new(@"[A-Za-z]+", RegexOptions.Compiled);

    public static string? GetFlag(string? location, string? organizer, string? title)
    {
        var code = TrailingCountryCode(location)
            ?? CountryCodeInWords(location)
            ?? CountryCodeInWords(organizer)
            ?? CountryCodeInWords(title);
        return code is null ? null : FlagEmoji(code);
    }

    private static string? TrailingCountryCode(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        var parts = location.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return null;
        var last = parts[^1];
        if (last.Length != 2 || !last.All(char.IsAsciiLetter)) return null;
        var code = last.ToUpperInvariant();
        return code == "UK" ? "GB" : code;
    }

    private static string? CountryCodeInWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        // Strip periods so dotted acronyms (U.S., U.K., U.S.A.) tokenise to US/UK/USA.
        var normalised = text.Replace(".", string.Empty);
        foreach (Match match in WordPattern.Matches(normalised))
        {
            if (Aliases.TryGetValue(match.Value, out var code)) return code;
        }
        return null;
    }

    private static string FlagEmoji(string code) =>
        string.Concat(code.Select(c => char.ConvertFromUtf32(0x1F1A5 + c)));
}

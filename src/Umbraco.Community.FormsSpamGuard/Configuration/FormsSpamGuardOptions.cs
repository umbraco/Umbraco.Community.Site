using Microsoft.Extensions.Logging;

namespace Umbraco.Community.FormsSpamGuard.Configuration;

/// <summary>
/// Site-wide options for the spam guard. Anything an editor should control per form lives on the field type as a
/// <c>[Setting]</c> instead; this type holds only what applies to the whole installation.
/// </summary>
public sealed class FormsSpamGuardOptions
{
    /// <summary>
    /// The ASP.NET Core Data Protection purpose string used to protect the render token. Changing this
    /// invalidates every token currently in flight, which is a safe (if abrupt) way to force re-issue.
    /// </summary>
    public string DataProtectionPurpose { get; set; } = "Umbraco.Community.FormsSpamGuard.v1";

    /// <summary>
    /// The level at which rejected submissions are logged. Rejections are expected traffic on a public site, so
    /// this can be turned down to <see cref="LogLevel.Information"/> or <see cref="LogLevel.Debug"/> once the
    /// thresholds are tuned.
    /// </summary>
    public LogLevel RejectionLogLevel { get; set; } = LogLevel.Warning;

    /// <summary>
    /// The level at which accepted submissions are logged. A rejection is otherwise the only outcome that leaves
    /// a trace, which makes "nothing was logged" ambiguous between "it passed" and "this field never ran at all"
    /// (e.g. every signal disabled, or the field misplaced on a non-final page). Defaults alongside the site's
    /// default minimum level so it is visible without extra configuration.
    /// </summary>
    public LogLevel AcceptanceLogLevel { get; set; } = LogLevel.Information;
}

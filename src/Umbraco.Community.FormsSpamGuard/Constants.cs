namespace Umbraco.Community.FormsSpamGuard;

/// <summary>
/// Constant values used across the spam guard package.
/// </summary>
public static class Constants
{
    /// <summary>
    /// The unique identifier of the spam guard field type. Stored against every form that uses the field, so it
    /// must never change once the package has been released.
    /// </summary>
    public const string FieldTypeId = "b1f0a5c4-8d3e-4b7a-9c2f-6e5d4a3b2c10";

    /// <summary>
    /// The field type alias, as persisted in form definitions.
    /// </summary>
    public const string FieldTypeAlias = "spamGuard";

    /// <summary>
    /// The configuration section the site-wide options bind from.
    /// </summary>
    public const string ConfigurationSection = "UmbracoCommunity:FormsSpamGuard";

    /// <summary>
    /// Suffixes appended to the field's HTML id to name the posted inputs. Kept short and non-descriptive so they
    /// do not advertise their purpose in the markup.
    /// </summary>
    public static class FormKeys
    {
        /// <summary>The protected token input.</summary>
        public const string TokenSuffix = "_sg";

        /// <summary>The JavaScript proof-of-presence input.</summary>
        public const string JavaScriptSuffix = "_sgjs";
    }

    /// <summary>
    /// Paths to the static assets shipped with the package.
    /// </summary>
    public static class Assets
    {
        private const string Root = "~/App_Plugins/UmbracoCommunityFormsSpamGuard";

        /// <summary>Stylesheet that hides the decoy field.</summary>
        public const string StyleSheet = $"{Root}/spam-guard.css";

        /// <summary>Script that fills the proof-of-presence input.</summary>
        public const string Script = $"{Root}/spam-guard.js";
    }
}

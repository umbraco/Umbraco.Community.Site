using UmbracoCommunity.Web.Utilities;

// Depends on Models Builder codegen: this partial only compiles once the FeedSubmissionBlock element
// type (IContentBlockIntro + Guidance RTE) and SettingsFeedSubmissionBlock settings type
// (ISettingsColour + ISettingsBlockId) have been created in the backoffice and models regenerated.
namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class FeedSubmissionBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);
    }
}

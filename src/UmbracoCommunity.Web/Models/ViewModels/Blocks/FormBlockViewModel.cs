using Umbraco.Cms.Core.Strings;
using UmbracoCommunity.Web.Models.PublishedModels;

namespace UmbracoCommunity.Web.Models.ViewModels.Blocks
{
    public class FormBlockViewModel
    {
        public FormBlockViewModel(FormBlock form)
        {
            FormId = form.Form.HasValue ? form.Form.Value : Guid.Empty;
            Title = form.Title;
            Subtitle = form.Subtitle;
        }

        public Guid FormId { get; init; }
        public string? Title { get; set; }
        public IHtmlEncodedString? Subtitle { get; set; }
    }
}

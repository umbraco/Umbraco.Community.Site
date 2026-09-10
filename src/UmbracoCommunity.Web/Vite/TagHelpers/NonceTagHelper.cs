using Joonasw.AspNetCore.SecurityHeaders.Csp;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace UmbracoCommunity.Web.Vite.TagHelpers
{
    [HtmlTargetElement("script", Attributes = "asp-add-nonce")]
    [HtmlTargetElement("style", Attributes = "asp-add-nonce")]
    [HtmlTargetElement("link", Attributes = "asp-add-nonce")]
    /// <summary>
    /// Adds the CSP nonce to elements marked <c>asp-add-nonce</c>.
    /// </summary>
    /// <remarks>
    /// Joonasw.AspNetCore.SecurityHeaders ships its own tag helper for the same attribute, and both are registered
    /// in _ViewImports, so both run. This one used <c>Attributes.Add</c>, which appends — so every element came out
    /// with <c>nonce</c> twice, and a duplicate attribute made the nonce ineffective: Chrome blocked every *inline*
    /// script as "violates ... nonce required", while <c>src</c>-based scripts survived only because
    /// <c>script-src</c> allows <c>'self'</c>. That hid the fault until an inline script was actually needed.
    ///
    /// <c>SetAttribute</c> replaces instead of appending, and running last (<see cref="Order"/>) means this one has
    /// the final say no matter which order the two helpers are invoked in. Kept rather than deleted because it also
    /// targets <c>style</c> and <c>link</c>, which the library's may not.
    /// </remarks>
    public class NonceTagHelper : TagHelper
    {
        private readonly ICspNonceService _nonceService;

        [HtmlAttributeName("asp-add-nonce")]
        public bool AddNonce { get; set; }

        /// <summary>Run after every other tag helper, so this one collapses any nonce another has already added.</summary>
        public override int Order => int.MaxValue;

        public NonceTagHelper(ICspNonceService nonceService) => _nonceService = nonceService;

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            if (AddNonce)
            {
                output.Attributes.SetAttribute("nonce", _nonceService.GetNonce());
            }
        }
    }
}

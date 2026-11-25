using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using UmbracoCommunity.Web.Utilities;

namespace UmbracoCommunity.Web.Models.PublishedModels
{
    public partial class PersonaPickerBlock
    {
        public string IdHash { get; } = StringUtilities.RandomString(5);

        public IReadOnlyList<Persona> Items =>
            _items ??= Personas?.Select(block => block.Content).OfType<Persona>()
                .Where(persona => !string.IsNullOrWhiteSpace(persona.PersonaName))
                .ToList()
            ?? [];

        private List<Persona>? _items;

        public string ResolvedSelectLabel => Fallback(SelectLabel, "I am a");
        public string ResolvedReasonsHeading => Fallback(ReasonsHeading, "What you bring back");
        public string ResolvedTestimonialsHeading => Fallback(TestimonialsHeading, "In their words");
        public string ResolvedEmailHeading => Fallback(EmailHeading, "Your email to your boss");

        public IReadOnlyList<(string? Heading, List<Persona> Personas)> RoleGroups() =>
            Items.GroupBy(persona => string.IsNullOrWhiteSpace(persona.RoleGroup) ? null : persona.RoleGroup.Trim())
                .Select(group => (group.Key, group.ToList()))
                .ToList();

        public Persona? DefaultPersona => RoleGroups().FirstOrDefault().Personas?.FirstOrDefault();

        public IReadOnlyDictionary<Persona, string> Slugs()
        {
            var slugs = new Dictionary<Persona, string>();
            var used = new HashSet<string>();
            foreach (var persona in Items)
            {
                var slug = ToSlug(persona.PersonaName);
                if (slug.Length == 0) slug = "persona";
                var unique = slug;
                for (var i = 2; !used.Add(unique); i++) unique = $"{slug}-{i}";
                slugs[persona] = unique;
            }

            return slugs;
        }

        public static string ToSlug(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;

            var folded = new StringBuilder();
            foreach (var c in value.Normalize(NormalizationForm.FormD))
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) folded.Append(c);
            }

            return Regex.Replace(folded.ToString().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        }

        private static string Fallback(string? value, string fallback) =>
            string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    public partial class Persona
    {
        public IEnumerable<TitleAndTextItem> ReasonItems =>
            Reasons?.Select(block => block.Content).OfType<TitleAndTextItem>().Where(item => !string.IsNullOrWhiteSpace(item.Title))
            ?? Enumerable.Empty<TitleAndTextItem>();

        public IEnumerable<Testimonial> TestimonialItems =>
            Testimonials?.OfType<Testimonial>().Where(item => !string.IsNullOrWhiteSpace(item.Quote))
            ?? Enumerable.Empty<Testimonial>();

        public string NormalisedEmailBody => (EmailBody ?? string.Empty).Replace("\r\n", "\n").Trim();

        public string MailtoUrl =>
            $"mailto:?subject={Uri.EscapeDataString(EmailSubject ?? string.Empty)}&body={Uri.EscapeDataString(NormalisedEmailBody.Replace("\n", "\r\n"))}";
    }

    public partial class Testimonial
    {
        public string Attribution => string.Join(", ", new[] { JobTitle, Company }.Where(part => !string.IsNullOrWhiteSpace(part)));
    }
}

using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace UmbracoCommunity.Web.Features.Profiles.Models;

/// <summary>
/// A piece of activity the member wants to add, as the account page posts it (<c>multipart/form-data</c>): the typed
/// fields, all as text so a bad value gets <see cref="ProfileActivityEntry.TryNormalize"/>'s message rather than a model
/// binding error, and an optional picture (see <see cref="ProfileActivityImageStore"/>).
/// </summary>
public class AddActivityEntryForm
{
    public string? Kind { get; set; }

    public string? Title { get; set; }

    public string? Url { get; set; }

    /// <summary>The date as <c>yyyy-MM-dd</c>.</summary>
    public string? Date { get; set; }

    public string? Description { get; set; }

    public IFormFile? Image { get; set; }

    /// <summary>The typed fields, with a date that isn't <c>yyyy-MM-dd</c> left out (so it is reported as missing).</summary>
    public AddActivityEntryRequest ToRequest() =>
        new(
            Kind,
            Title,
            Url,
            DateOnly.TryParseExact(Date?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date : null,
            Description);
}

/// <summary>
/// An edit of an entry the member added, posted like <see cref="AddActivityEntryForm"/>. A new <see cref="AddActivityEntryForm.Image"/>
/// replaces the picture; without one, <see cref="RemoveImage"/> removes it, and otherwise the picture is kept.
/// </summary>
public sealed class UpdateActivityEntryForm : AddActivityEntryForm
{
    public bool RemoveImage { get; set; }
}

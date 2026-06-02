namespace Umbraco.Community.NotFoundTracker.Models.Entities;

public enum HitStatus : byte
{
    Active = 0,
    IgnoredManually = 1,
    Redirected = 2,
}

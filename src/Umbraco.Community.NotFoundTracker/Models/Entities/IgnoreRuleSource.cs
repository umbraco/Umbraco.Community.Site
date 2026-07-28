namespace Umbraco.Community.NotFoundTracker.Models.Entities;

public enum IgnoreRuleSource : byte
{
    UserDefined = 0,
    AutoPreset = 1,
    ConfigSeeded = 2,
}

namespace UmbracoCommunity.Common.Utilities;

public static class StringUtilities
{
    private const string Charbase = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

    public static string RandomString(int length)
    {
        Random rand = new();

        return new string(Enumerable.Range(0, length)
               .Select(_ => Charbase[rand.Next(Charbase.Length)])
               .ToArray());
    }
}

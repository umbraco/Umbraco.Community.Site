using Joonasw.AspNetCore.SecurityHeaders.Csp.Builder;

namespace UmbracoCommunity.Web.Extensions;
public static class CspBuilderExtensions
{
    public static CspConnectionBuilder ToAll(this CspConnectionBuilder builder, params string[] domains)
    {
        foreach (var domain in domains)
        {
            builder.To(domain);
        }

        return builder;
    }

    public static TBuilder FromAll<TBuilder, T>(this TBuilder builder, Func<TBuilder, string, T> fromMethod, params string[] domains)
    {
        foreach (var domain in domains)
        {
            fromMethod(builder, domain);
        }

        return builder;
    }
}

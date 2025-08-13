using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace UmbracoCommunity.Web.Extensions
{
    public static class WebHostEnvironmentExtensions
    {
        public static bool IsDevelopmentEnvironment(this IWebHostEnvironment env)
            => env.IsDevelopment() || env.IsLocalEnvironment();

        public static bool IsLocalEnvironment(this IWebHostEnvironment env)
            => env.IsEnvironment("Local");
    }
}

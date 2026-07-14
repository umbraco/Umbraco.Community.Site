using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Web.Common.ApplicationBuilder;

namespace UmbracoCommunity.Web.Features.Feeds.CommunityBlogs;

/// <summary>
/// Registers the per-IP rate-limit policy applied to <c>FeedSubmissionApiController</c> and wires
/// <c>UseRateLimiter</c> into the Umbraco pipeline after routing (so endpoint metadata is available)
/// but before endpoints execute. This is the only rate limiting in the solution and is scoped to the
/// feed-submission endpoints via <c>[EnableRateLimiting]</c>; it is not applied globally.
/// </summary>
public sealed class FeedSubmissionRateLimiting : IComposer
{
    public const string PolicyName = "feed-submission";

    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(PolicyName, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 60,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        builder.Services.Configure<UmbracoPipelineOptions>(pipeline =>
        {
            pipeline.AddFilter(new UmbracoPipelineFilter(
                nameof(FeedSubmissionRateLimiting),
                postRouting: app => app.UseRateLimiter()));
        });
    }
}

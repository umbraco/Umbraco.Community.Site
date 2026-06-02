// Cloud redeploy trigger: 2026-05-21 (after raising SCM_COMMAND_IDLE_TIMEOUT).
using UmbracoCommunity.Web.Extensions;
using UmbracoCommunity.Web.Middleware;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddOutputCaching()
    .AddSecurityPolicies()
    .AddViewModelBuildersAndDecorators()
    .AddPipelineFilters()
    .Build();

builder.Services.AddResponseCaching();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error.html");
    app.UseHsts();
}

app.UseWhen(
    // Media requests excluded from response caching because of ImageSharp caching duplication and issues with caching query-parametrized images.
    n => !n.Request.Path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.UseResponseCaching()
);

// Long-cache /media responses at the browser/CDN. Umbraco's GetCropUrl
// helpers append ?v=<hash> so most image URLs change on edit; for stable
// direct-path URLs (e.g. the header logo) we omit "immutable" so browsers
// can still revalidate via ETag/Last-Modified after max-age.
app.UseWhen(
    n => n.Request.Path.StartsWithSegments("/media", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode is >= 200 and < 400)
            {
                context.Response.Headers["Cache-Control"] = "public, max-age=31536000";
            }
            return Task.CompletedTask;
        });
        return next();
    })
);

// Vite emits content-hashed filenames under /assets (e.g. _index-BPFN5C33.css)
// so they are safe to mark immutable. manifest.json is not hashed and is read
// server-side, so we let it revalidate normally.
app.UseWhen(
    n => n.Request.Path.StartsWithSegments("/assets", StringComparison.OrdinalIgnoreCase)
        && !n.Request.Path.Value!.EndsWith("/manifest.json", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode is >= 200 and < 400)
            {
                context.Response.Headers["Cache-Control"] = "public, max-age=31536000, immutable";
            }
            return Task.CompletedTask;
        });
        return next();
    })
);

// Fonts under /fonts use stable filenames, so long max-age without immutable
// keeps revalidation available if a file is ever replaced.
app.UseWhen(
    n => n.Request.Path.StartsWithSegments("/fonts", StringComparison.OrdinalIgnoreCase),
    appBuilder => appBuilder.Use((context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            if (context.Response.StatusCode is >= 200 and < 400)
            {
                context.Response.Headers["Cache-Control"] = "public, max-age=31536000";
            }
            return Task.CompletedTask;
        });
        return next();
    })
);

await app.BootUmbracoAsync();

app.UseHttpsRedirection();

app.UseSecurityHeaders();

app.UseMiddleware<FormValidationMiddleware>();

app.UseOutputCache();

app.UseUmbraco()
    .WithMiddleware(u =>
    {
        u.UseBackOffice();
        u.UseWebsite();
    })
    .WithEndpoints(u =>
    {
        u.UseBackOfficeEndpoints();
        u.UseApplicationEndpoints();
        u.UseWebsiteEndpoints();
    });

await app.RunAsync();

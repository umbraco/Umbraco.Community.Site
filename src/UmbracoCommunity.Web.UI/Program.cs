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
            context.Response.Headers["Cache-Control"] = "public, max-age=31536000";
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

using UmbracoCommunity.Web.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.CreateUmbracoBuilder()
    .AddBackOffice()
    .AddWebsite()
    .AddComposers()
    .AddSecurityPolicies()
    .AddViewModelBuildersAndDecorators()
    .AddPipelineFilters()
    .Build();

WebApplication app = builder.Build();

await app.BootUmbracoAsync();

app.UseHttpsRedirection();

app.UseSecurityHeaders();

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

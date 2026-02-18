using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using UmbracoCommunity.BlockRestrictions.Infrastructure;

namespace UmbracoCommunity.BlockRestrictions;

public class BlockRestrictionComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Services.AddDbContextFactory<BlockRestrictionDbContext>((serviceProvider, options) =>
        {
            var connectionString = builder.Config["ConnectionStrings:umbracoDbDSN"];
            var providerName = builder.Config["ConnectionStrings:umbracoDbDSN_ProviderName"];

            if (providerName == "Microsoft.Data.Sqlite")
            {
                var hostingEnvironment = serviceProvider
                    .GetRequiredService<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
                var dataDirectory = Path.Combine(hostingEnvironment.ContentRootPath, "umbraco", "Data");
                var resolvedConnectionString = connectionString?.Replace("|DataDirectory|", dataDirectory);

                options.UseSqlite(resolvedConnectionString, sqliteOptions =>
                {
                    sqliteOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
                });
            }
            else
            {
                options.UseSqlServer(connectionString, sqlOptions =>
                {
                    sqlOptions.MigrationsAssembly("UmbracoCommunity.BlockRestrictions");
                });
            }

            options.ConfigureWarnings(warnings =>
                warnings.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        builder.Services.Configure<SwaggerGenOptions>(opt =>
        {
            opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
            {
                Title = "Umbraco Community Block Restrictions Backoffice API",
                Version = "1.0",
            });

            opt.OperationFilter<BlockRestrictionsOperationSecurityFilter>();
        });

        builder.Services.AddHostedService<BlockRestrictionMigrationHostedService>();
        builder.Services.AddScoped<BlockRestrictionStore>();
        builder.Services.AddScoped<BlockRestrictionService>();
    }

    private class BlockRestrictionsOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
    {
        protected override string ApiName => Constants.ApiName;
    }
}

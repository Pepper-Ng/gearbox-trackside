using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Trackside.Application.Serialization;
using Trackside.Host.Api;
using Trackside.Host.Configuration;
using Trackside.Host.Hubs;

namespace Trackside.Host.Hosting;

/// <summary>
/// Builds the ASP.NET Core application and keeps endpoint composition in one place.
/// </summary>
public static class TracksideWebApplication
{
    /// <summary>
    /// Creates the configured web application without starting it.
    /// </summary>
    /// <param name="args">Command-line arguments that override normal configuration sources.</param>
    /// <returns>A configured <see cref="WebApplication" /> ready to run.</returns>
    public static WebApplication Create(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = TracksideCommandLine.Normalize(args),
            ContentRootPath = AppContext.BaseDirectory,
        });

        var listenUrl = builder.Configuration.GetSection(TracksideOptions.SectionName)
            .GetSection(nameof(TracksideOptions.Http))
            .GetValue<string>(nameof(TracksideHttpOptions.ListenUrl)) ?? TracksideHttpOptions.DefaultListenUrl;

        builder.WebHost.UseUrls(listenUrl);
        builder.Services.AddTracksideFoundation(builder.Configuration);
        builder.Services.Configure<JsonOptions>(options => TracksideJson.Configure(options.SerializerOptions));
        builder.Services.AddSignalR().AddJsonProtocol(options => TracksideJson.Configure(options.PayloadSerializerOptions));
        builder.Services.AddCors(options =>
        {
            options.AddPolicy(TracksideCors.PolicyName, policy =>
            {
                var origins = builder.Configuration
                    .GetSection($"{TracksideOptions.SectionName}:{nameof(TracksideOptions.Cors)}:{nameof(TracksideCorsOptions.AllowedOrigins)}")
                    .Get<string[]>() ?? [];

                if (origins.Length == 0)
                {
                    policy.SetIsOriginAllowed(_ => false);
                    return;
                }

                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        var app = builder.Build();

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(TracksideCors.PolicyName);
        app.MapTracksideApi();
        app.MapHub<LiveSessionHub>(LiveSessionRoutes.HubPath);
        app.MapFallbackToFile("index.html");

        return app;
    }
}

/// <summary>
/// Names the CORS policy used by the development kiosk frontend.
/// </summary>
public static class TracksideCors
{
    /// <summary>
    /// CORS policy name for local browser clients running outside the ASP.NET Core origin.
    /// </summary>
    public const string PolicyName = "TracksideKioskClient";
}
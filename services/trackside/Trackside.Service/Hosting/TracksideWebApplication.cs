using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Trackside.Application.Serialization;
using Trackside.Service.Api;
using Trackside.Service.Configuration;
using Trackside.Service.Hubs;

namespace Trackside.Service.Hosting;

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
        var normalizedArgs = TracksideCommandLine.Normalize(args, out var forceConsoleMode, out var configRoot);
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = normalizedArgs,
            ContentRootPath = AppContext.BaseDirectory,
        });

        var isWindowsService = !forceConsoleMode && OperatingSystem.IsWindows() && WindowsServiceHelpers.IsWindowsService();
        if (!forceConsoleMode && OperatingSystem.IsWindows())
        {
            builder.Host.UseWindowsService(options => options.ServiceName = "Trackside");
        }

        AddWritableLocalConfiguration(builder, normalizedArgs);
        AddExternalConfiguration(builder, configRoot, normalizedArgs);

        var listenUrl = builder.Configuration.GetSection(TracksideOptions.SectionName)
            .GetSection(nameof(TracksideOptions.Http))
            .GetValue<string>(nameof(TracksideHttpOptions.ListenUrl)) ?? TracksideHttpOptions.DefaultListenUrl;

        builder.WebHost.UseUrls(listenUrl);
        builder.Services.AddSingleton(new TracksideRuntimeContext(forceConsoleMode, isWindowsService, builder.Environment.ContentRootPath, configRoot));
        builder.Services.AddTracksideFoundation(builder.Configuration);
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "Trackside.Admin";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.LoginPath = "/config";
                options.AccessDeniedPath = "/config";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    if (context.Request.Path.StartsWithSegments("/api"))
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        return Task.CompletedTask;
                    }

                    context.Response.Redirect(context.RedirectUri);
                    return Task.CompletedTask;
                };
            });
        builder.Services.AddAuthorization();
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

        var configurationPagePath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "configuration.html");
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? string.Empty;
            if (path.Equals("/config", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/config/", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.ContentType = "text/html; charset=utf-8";
                await context.Response.SendFileAsync(configurationPagePath);
                return;
            }

            await next();
        });

        app.UseDefaultFiles();
        app.UseStaticFiles();
        app.UseCors(TracksideCors.PolicyName);
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapTracksideApi();
        app.MapHub<LiveSessionHub>(LiveSessionRoutes.HubPath);
        app.MapFallbackToFile("index.html");

        return app;
    }

    private static void AddExternalConfiguration(WebApplicationBuilder builder, string? configRoot, string[] normalizedArgs)
    {
        if (string.IsNullOrWhiteSpace(configRoot))
        {
            return;
        }

        var serviceConfigRoot = Path.Combine(configRoot, "service");
        builder.Configuration
            .AddJsonFile(Path.Combine(serviceConfigRoot, "appsettings.json"), optional: true, reloadOnChange: true)
            .AddJsonFile(Path.Combine(serviceConfigRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(normalizedArgs);
    }

    private static void AddWritableLocalConfiguration(WebApplicationBuilder builder, string[] normalizedArgs)
    {
        builder.Configuration
            .AddJsonFile(Path.Combine(builder.Environment.ContentRootPath, "appsettings.Local.json"), optional: true, reloadOnChange: true)
            .AddEnvironmentVariables()
            .AddCommandLine(normalizedArgs);
    }
}

/// <summary>
/// Process-level runtime details that are not pure configuration.
/// </summary>
public sealed record TracksideRuntimeContext(bool ForceConsoleMode, bool IsWindowsService, string ContentRootPath, string? ExternalConfigRoot)
{
    /// <summary>
    /// Runtime state surfaced through health responses.
    /// </summary>
    public string ServiceState => ForceConsoleMode ? "Console" : IsWindowsService ? "WindowsService" : "Interactive";
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
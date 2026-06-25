using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Trackside.Application.Configuration;
using Trackside.Application.LiveSession;
using Trackside.Infrastructure.Rf2.SharedMemory;
using Trackside.Service.Security;
using Trackside.Service.Workers;

namespace Trackside.Service.Configuration;

/// <summary>
/// Registers Trackside services used by the API, SignalR hub, and hosted workers.
/// </summary>
public static class TracksideServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Phase 0B application foundation to the ASP.NET Core service container.
    /// </summary>
    /// <param name="services">Service collection receiving Trackside registrations.</param>
    /// <param name="configuration">Application configuration used for strongly typed options.</param>
    /// <returns>The same service collection for call chaining.</returns>
    public static IServiceCollection AddTracksideFoundation(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<TracksideOptions>()
            .Bind(configuration.GetSection(TracksideOptions.SectionName))
            .Validate(options => options.Source.Mode != LiveSessionSourceMode.Fixture || !string.IsNullOrWhiteSpace(options.Source.FixturePath),
                "Fixture mode requires Trackside:Source:FixturePath.")
            .Validate(options => options.LiveSession.PublishIntervalSeconds is >= TracksideLiveSessionOptions.MinimumPublishIntervalSeconds and <= 60.0,
                "Trackside:LiveSession:PublishIntervalSeconds must be between 0.25 and 60 seconds.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Deployment.InstallMode),
                "Trackside:Deployment:InstallMode is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.Deployment.ServiceName),
                "Trackside:Deployment:ServiceName is required.")
            .ValidateOnStart();

        services.AddOptions<TracksideSourceOptions>()
            .Bind(configuration.GetSection($"{TracksideOptions.SectionName}:{nameof(TracksideOptions.Source)}"))
            .ValidateOnStart();

        services.AddOptions<TracksideLiveSessionOptions>()
            .Bind(configuration.GetSection($"{TracksideOptions.SectionName}:{nameof(TracksideOptions.LiveSession)}"))
            .ValidateOnStart();

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<LiveSessionState>();
        services.AddSingleton<TracksideSourceConfigurationStore>();
        services.AddSingleton<AdminUserStore>();
        services.AddSingleton<ILeaderboardSnapshotBuilder, LeaderboardSnapshotBuilder>();
        services.AddSingleton<MappedBufferPayloadLocator>();
        services.AddSingleton<Rf2SharedMemoryMapReader>();
        services.AddSingleton<IRf2SharedMemoryMapDiscovery, Rf2SharedMemoryMapDiscovery>();
        services.AddSingleton<Rf2ScoringMapResolver>();
        services.AddSingleton<IRf2ScoringPayloadParser, Rf2ScoringPayloadParser>();
        services.AddSingleton<ILiveSessionSource, ReloadingLiveSessionSource>();
        services.AddHostedService<LiveSessionPublisher>();

        return services;
    }
}
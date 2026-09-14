using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NeteaseMusicCloudMatch.Core.Services;
using NeteaseMusicCloudMatch.Infrastructure.Api;
using NeteaseMusicCloudMatch.Infrastructure.Authentication;
using NeteaseMusicCloudMatch.Infrastructure.Services;

namespace NeteaseMusicCloudMatch.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNeteaseMusicCloudMatchInfrastructure(
        this IServiceCollection services,
        Action<NeteaseApiOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }
        else
        {
            services.Configure<NeteaseApiOptions>(_ => { });
        }

        services.AddHttpClient<INeteaseApiClient, NeteaseApiClient>((serviceProvider, client) =>
            {
                var options = serviceProvider.GetRequiredService<IOptions<NeteaseApiOptions>>().Value;
                client.BaseAddress = options.BaseUri;
                client.Timeout = options.Timeout;
            })
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
                PooledConnectionLifetime = TimeSpan.FromMinutes(10),
                UseCookies = false
            });

        services.AddSingleton<ICookieStore, DpapiCookieStore>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<IAuthService>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddSingleton<IAuthSessionAccessor>(serviceProvider => serviceProvider.GetRequiredService<AuthService>());
        services.AddTransient<ICloudDriveService, CloudDriveService>();
        services.AddTransient<ISongService, SongService>();
        services.AddSingleton<ICloudMatchService, CloudMatchService>();
        return services;
    }
}

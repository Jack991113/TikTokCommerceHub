using System.IO.Compression;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using TikTokCommerceHub.Application.Abstractions;
using TikTokCommerceHub.Application.Dashboard;
using TikTokCommerceHub.Infrastructure.Dashboard;
using TikTokCommerceHub.Infrastructure.Options;

namespace TikTokCommerceHub.Web.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCommerceHubCore(this IServiceCollection services, IConfiguration configuration)
    {
        var options = new CurrentSuiteOptions();
        configuration.GetSection(CurrentSuiteOptions.SectionName).Bind(options);
        services.AddSingleton(options);
        services.Configure<JsonOptions>(static options =>
        {
            options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        });
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IStoreSnapshotReader, RuntimeStateStoreSnapshotReader>();
        services.AddSingleton<IDashboardSnapshotService, DashboardSnapshotService>();
        services.AddMemoryCache();
        services.AddProblemDetails();
        services.AddOutputCache(options =>
        {
            options.AddBasePolicy(policy => policy.Expire(TimeSpan.FromSeconds(20)));
        });
        services.AddResponseCompression(options =>
        {
            options.EnableForHttps = true;
            options.Providers.Add<BrotliCompressionProvider>();
            options.Providers.Add<GzipCompressionProvider>();
        });
        services.Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter("dashboard", limiter =>
            {
                limiter.PermitLimit = 30;
                limiter.Window = TimeSpan.FromSeconds(10);
                limiter.QueueLimit = 10;
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            });
        });
        services.AddHealthChecks();
        return services;
    }
}

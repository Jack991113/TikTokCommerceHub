using Microsoft.AspNetCore.OutputCaching;
using TikTokCommerceHub.Application.Dashboard;

namespace TikTokCommerceHub.Web.Endpoints;

public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/dashboard")
            .WithTags("Dashboard")
            .RequireRateLimiting("dashboard");

        group.MapGet("/snapshot", async (
            IDashboardSnapshotService snapshotService,
            CancellationToken cancellationToken) =>
        {
            var snapshot = await snapshotService.GetAsync(cancellationToken);
            return Results.Ok(snapshot);
        })
        .CacheOutput(policy => policy.Expire(TimeSpan.FromSeconds(20)))
        .WithName("GetDashboardSnapshot");

        return app;
    }
}

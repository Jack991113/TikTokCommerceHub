using TikTokCommerceHub.Web.DependencyInjection;
using TikTokCommerceHub.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCommerceHubCore(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseOutputCache();
app.UseRateLimiter();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapDashboardEndpoints();

app.Run();

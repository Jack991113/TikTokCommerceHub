using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TikTokSalesStats.Models;
using TikTokSalesStats.Options;

namespace TikTokSalesStats.Services;

public sealed class TikTokSalesApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TikTokRequestSigner _signer;
    private readonly SalesStatsOptions _options;
    private readonly ILogger<TikTokSalesApiClient> _logger;

    public TikTokSalesApiClient(
        IHttpClientFactory httpClientFactory,
        TikTokRequestSigner signer,
        IOptions<SalesStatsOptions> options,
        ILogger<TikTokSalesApiClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<List<JsonObject>> SearchOrdersAsync(
        RuntimeStateSnapshot storeState,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken)
    {
        var configuration = ResolveConfiguration(storeState);
        var results = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var mode in new[] { "create", "update" })
        {
            var nextPageToken = string.Empty;

            do
            {
                var body = new JsonObject();
                if (string.Equals(mode, "create", StringComparison.Ordinal))
                {
                    body["create_time_ge"] = fromUtc.ToUnixTimeSeconds();
                    body["create_time_lt"] = toUtc.ToUnixTimeSeconds();
                }
                else
                {
                    body["update_time_ge"] = fromUtc.ToUnixTimeSeconds();
                    body["update_time_lt"] = toUtc.ToUnixTimeSeconds();
                }

                var queryParameters = new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["shop_cipher"] = configuration.ShopCipher,
                    ["page_size"] = Math.Clamp(_options.PageSize, 1, 100).ToString(CultureInfo.InvariantCulture),
                    ["sort_field"] = string.Equals(mode, "create", StringComparison.Ordinal) ? "create_time" : "update_time",
                    ["sort_order"] = "DESC"
                };

                if (!string.IsNullOrWhiteSpace(nextPageToken))
                {
                    queryParameters["page_token"] = nextPageToken;
                }

                var response = await SendBusinessRequestAsync(
                    configuration,
                    HttpMethod.Post,
                    "/order/202309/orders/search",
                    queryParameters,
                    body,
                    cancellationToken);

                var data = response["data"] as JsonObject;
                if (data?["orders"] is JsonArray orders)
                {
                    foreach (var node in orders.OfType<JsonObject>())
                    {
                        var orderId = ExtractString(node, "id");
                        if (!string.IsNullOrWhiteSpace(orderId))
                        {
                            results[orderId] = node;
                        }
                    }
                }

                nextPageToken = ExtractString(data, "next_page_token") ?? string.Empty;
            }
            while (!string.IsNullOrWhiteSpace(nextPageToken));
        }

        return results.Values.ToList();
    }

    private async Task<JsonObject> SendBusinessRequestAsync(
        StoreApiConfiguration configuration,
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?> queryParameters,
        JsonObject body,
        CancellationToken cancellationToken)
    {
        var bodyJson = body.ToJsonString(SerializerOptions);

        try
        {
            return await SendBusinessRequestCoreAsync(configuration, method, path, queryParameters, bodyJson, configuration.AccessToken, cancellationToken);
        }
        catch (InvalidOperationException ex) when (_options.AutoRefreshAccessToken && ShouldRefreshAccessToken(ex) && !string.IsNullOrWhiteSpace(configuration.RefreshToken))
        {
            _logger.LogWarning(ex, "TikTok access token for {StoreName} may be expired. Refreshing in-memory token for sales query.", configuration.StoreName);
            var refreshedAccessToken = await RefreshAccessTokenAsync(configuration, cancellationToken);
            return await SendBusinessRequestCoreAsync(configuration, method, path, queryParameters, bodyJson, refreshedAccessToken, cancellationToken);
        }
    }

    private async Task<JsonObject> SendBusinessRequestCoreAsync(
        StoreApiConfiguration configuration,
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?> queryParameters,
        string bodyJson,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestUri = _signer.BuildSignedUri(
            _options.ApiBaseUrl,
            path,
            configuration.AppKey,
            configuration.AppSecret,
            queryParameters,
            bodyJson);

        using var request = new HttpRequestMessage(method, requestUri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Content = new StringContent(bodyJson, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("x-tts-access-token", accessToken);
        return await SendForJsonAsync(request, cancellationToken);
    }

    private async Task<string> RefreshAccessTokenAsync(StoreApiConfiguration configuration, CancellationToken cancellationToken)
    {
        var requestBody = new JsonObject
        {
            ["app_key"] = configuration.AppKey,
            ["app_secret"] = configuration.AppSecret,
            ["refresh_token"] = configuration.RefreshToken,
            ["grant_type"] = "refresh_token"
        };

        var refreshUri = BuildAuthUri(_options.AuthBaseUrl, _options.TokenRefreshPath);
        using var request = new HttpRequestMessage(HttpMethod.Post, refreshUri)
        {
            Version = HttpVersion.Version11,
            VersionPolicy = HttpVersionPolicy.RequestVersionOrLower,
            Content = JsonContent.Create(requestBody)
        };

        var response = await SendForJsonAsync(request, cancellationToken);
        var data = response["data"] as JsonObject ?? response;
        var refreshedAccessToken = ExtractString(data, "access_token");
        if (string.IsNullOrWhiteSpace(refreshedAccessToken))
        {
            throw new InvalidOperationException("TikTok 刷新 Token 成功，但返回里没有 access_token。");
        }

        return refreshedAccessToken;
    }

    private async Task<JsonObject> SendForJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClientFactory.CreateClient("TikTokStatsApi").SendAsync(request, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"TikTok API request failed with {(int)response.StatusCode} {response.ReasonPhrase}. Body: {content}");
        }

        var root = JsonNode.Parse(content) as JsonObject
            ?? throw new InvalidOperationException("TikTok API response was not a JSON object.");

        var code = root["code"]?.GetValue<int?>();
        if (code is not null && code.Value != 0)
        {
            var message = ExtractString(root, "message") ?? ExtractString(root, "msg") ?? "Unknown TikTok API error.";
            throw new InvalidOperationException($"TikTok API returned code {code}: {message}");
        }

        return root;
    }

    private static bool ShouldRefreshAccessToken(InvalidOperationException exception) =>
        exception.Message.Contains("401", StringComparison.Ordinal) ||
        exception.Message.Contains("access token", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("\"code\":105001", StringComparison.Ordinal) ||
        exception.Message.Contains("\"code\":105004", StringComparison.Ordinal);

    private static StoreApiConfiguration ResolveConfiguration(RuntimeStateSnapshot state)
    {
        if (string.IsNullOrWhiteSpace(state.AppKey) ||
            string.IsNullOrWhiteSpace(state.AppSecret) ||
            string.IsNullOrWhiteSpace(state.AccessToken) ||
            string.IsNullOrWhiteSpace(state.ShopId))
        {
            throw new InvalidOperationException($"店铺 {state.StoreName} 缺少 TikTok API 凭证，无法拉取统计数据。");
        }

        return new StoreApiConfiguration(
            state.StoreName,
            state.AppKey.Trim(),
            state.AppSecret.Trim(),
            state.AccessToken.Trim(),
            state.RefreshToken?.Trim() ?? string.Empty,
            state.ShopId.Trim());
    }

    private static Uri BuildAuthUri(string authBaseUrl, string path)
    {
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri))
        {
            return absoluteUri;
        }

        var normalizedBaseUrl = authBaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? authBaseUrl
            : $"{authBaseUrl}/";

        return new Uri(new Uri(normalizedBaseUrl), path.TrimStart('/'));
    }

    private static string? ExtractString(JsonObject? node, string propertyName) =>
        node?[propertyName]?.GetValue<string?>() ?? node?[propertyName]?.ToString();

    private sealed record StoreApiConfiguration(
        string StoreName,
        string AppKey,
        string AppSecret,
        string AccessToken,
        string RefreshToken,
        string ShopCipher);
}

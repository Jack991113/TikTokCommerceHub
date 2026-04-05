using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;

namespace TikTokOrderPrinter.Services;

public sealed class TikTokShopClient
{
    private const string EmptyAuthCodeMessage = "\u6388\u6743\u7801\u4E3A\u7A7A\uFF0C\u8BF7\u7C98\u8D34\u56DE\u8C03\u5730\u5740\u91CC\u7684 code\uFF0C\u6216\u8005\u76F4\u63A5\u7C98\u8D34\u5B8C\u6574\u56DE\u8C03\u5730\u5740\u3002";
    private const string WrongAuthInputMessage = "\u8FD9\u91CC\u9700\u8981\u7C98\u8D34\u6388\u6743\u56DE\u8C03\u91CC\u7684 code\uFF0C\u4E0D\u662F Access Token\u3002";
    private const string MissingReturnedTokensMessage = "TikTok \u8FD4\u56DE\u6210\u529F\uFF0C\u4F46\u6CA1\u6709\u5E26\u56DE\u5B8C\u6574\u7684 Access Token / Refresh Token\u3002";
    private const string ExchangeTokenSuccessMessage = "TikTok \u6388\u6743\u7801\u6362\u53D6 Token \u6210\u529F\u3002";
    private const string MissingRefreshResultMessage = "TikTok \u5237\u65B0 Token \u7684\u8FD4\u56DE\u91CC\u6CA1\u6709 Access Token\u3002";
    private const string RefreshTokenSuccessMessage = "TikTok Access Token \u5237\u65B0\u6210\u529F\u3002";
    private const string MissingAccessTokenMessage = "\u7F3A\u5C11 Access Token\uFF0C\u8BF7\u5148\u5728\u754C\u9762\u91CC\u586B\u5199 Token\uFF0C\u6216\u7528\u6388\u6743\u7801\u6362\u53D6 Token\u3002";
    private const string MissingAppCredentialsMessage = "\u7F3A\u5C11 App Key \u6216 App Secret\uFF0C\u8BF7\u5148\u5728\u754C\u9762\u91CC\u586B\u5199\u5E76\u4FDD\u5B58\u3002";
    private const string MissingShopIdentifierMessage = "Fill Shop ID / Shop Cipher first. If you only have a numeric Shop ID, the current token must also be allowed to read authorized shops so the app can resolve the matching shop_cipher.";
    private const string NoAuthorizedShopsMessage = "No authorized TikTok shops were returned for the current access token.";
    private const string ShopCipherResolutionFailedMessage = "Unable to resolve shop_cipher from the current configuration. Paste the actual shop_cipher into the Shop ID / Shop Cipher field.";
    private const string ShopCipherPermissionRequiredMessage = "The current access token cannot call Get Authorized Shops, so the app cannot convert a numeric Shop ID into shop_cipher. Paste the actual shop_cipher into the Shop ID / Shop Cipher field, or grant that permission and authorize again.";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TikTokRequestSigner _signer;
    private readonly RuntimeStateStore _stateStore;
    private readonly IOptions<TikTokShopOptions> _options;
    private readonly ILogger<TikTokShopClient> _logger;
    private const int MaxSendAttempts = 3;

    public TikTokShopClient(
        IHttpClientFactory httpClientFactory,
        TikTokRequestSigner signer,
        RuntimeStateStore stateStore,
        IOptions<TikTokShopOptions> options,
        ILogger<TikTokShopClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _signer = signer;
        _stateStore = stateStore;
        _options = options;
        _logger = logger;
    }

    public async Task<JsonObject> SearchOrdersAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken)
    {
        var configuration = await GetResolvedConfigurationAsync(cancellationToken);
        EnsureAppCredentials(configuration);
        EnsureAccessToken(configuration);

        var shopCipher = await ResolveShopCipherAsync(configuration, cancellationToken);
        var orders = new JsonArray();
        var nextPageToken = string.Empty;
        var totalCount = 0;
        var totalCountInitialized = false;

        do
        {
            var body = new JsonObject
            {
                ["update_time_ge"] = fromUtc.ToUnixTimeSeconds(),
                ["update_time_lt"] = toUtc.ToUnixTimeSeconds()
            };

            var queryParameters = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["shop_cipher"] = shopCipher,
                ["page_size"] = _options.Value.PageSize.ToString(CultureInfo.InvariantCulture),
                ["sort_field"] = "update_time",
                ["sort_order"] = "ASC"
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
            if (data?["orders"] is JsonArray pageOrders)
            {
                foreach (var order in pageOrders)
                {
                    if (order is not null)
                    {
                        orders.Add(order.DeepClone());
                    }
                }
            }

            if (!totalCountInitialized)
            {
                totalCount = data?["total_count"]?.GetValue<int?>() ?? 0;
                totalCountInitialized = true;
            }

            nextPageToken = ExtractString(data, "next_page_token") ?? string.Empty;
        }
        while (!string.IsNullOrWhiteSpace(nextPageToken));

        return new JsonObject
        {
            ["data"] = new JsonObject
            {
                ["orders"] = orders,
                ["total_count"] = totalCount
            }
        };
    }

    public async Task<JsonObject> SearchOrdersPageAsync(OrderQueryRequest request, CancellationToken cancellationToken)
    {
        var configuration = await GetResolvedConfigurationAsync(cancellationToken);
        EnsureAppCredentials(configuration);
        EnsureAccessToken(configuration);

        var fromUtc = request.FromUtc ?? DateTimeOffset.UtcNow.AddDays(-30);
        var toUtc = request.ToUtc ?? DateTimeOffset.UtcNow;
        if (fromUtc >= toUtc)
        {
            throw new InvalidOperationException("历史订单查询的开始时间必须早于结束时间。");
        }

        var shopCipher = await ResolveShopCipherAsync(configuration, cancellationToken);
        var body = new JsonObject
        {
            ["create_time_ge"] = fromUtc.ToUnixTimeSeconds(),
            ["create_time_lt"] = toUtc.ToUnixTimeSeconds()
        };

        var pageSize = Math.Clamp(request.PageSize, 1, 100);
        var queryParameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["shop_cipher"] = shopCipher,
            ["page_size"] = pageSize.ToString(CultureInfo.InvariantCulture),
            ["sort_field"] = string.IsNullOrWhiteSpace(request.SortField) ? "create_time" : request.SortField.Trim(),
            ["sort_order"] = string.IsNullOrWhiteSpace(request.SortOrder) ? "DESC" : request.SortOrder.Trim().ToUpperInvariant()
        };

        if (!string.IsNullOrWhiteSpace(request.PageToken))
        {
            queryParameters["page_token"] = request.PageToken.Trim();
        }

        return await SendBusinessRequestAsync(
            configuration,
            HttpMethod.Post,
            "/order/202309/orders/search",
            queryParameters,
            body,
            cancellationToken);
    }

    public async Task<JsonObject> GetOrderDetailAsync(string orderId, CancellationToken cancellationToken)
    {
        var configuration = await GetResolvedConfigurationAsync(cancellationToken);
        EnsureAppCredentials(configuration);
        EnsureAccessToken(configuration);

        var shopCipher = await ResolveShopCipherAsync(configuration, cancellationToken);
        var queryParameters = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["shop_cipher"] = shopCipher,
            ["ids"] = orderId
        };

        return await SendBusinessRequestAsync(
            configuration,
            HttpMethod.Get,
            "/order/202507/orders",
            queryParameters,
            body: null,
            cancellationToken);
    }

    public async Task<JsonObject> ExchangeAuthorizationCodeAsync(string authCodeOrUrl, CancellationToken cancellationToken)
    {
        var configuration = await GetResolvedConfigurationAsync(cancellationToken);
        EnsureAppCredentials(configuration);

        var authCode = NormalizeAuthorizationCode(authCodeOrUrl);
        if (string.IsNullOrWhiteSpace(authCode))
        {
            throw new InvalidOperationException(EmptyAuthCodeMessage);
        }

        if (LooksLikeAccessToken(authCode))
        {
            throw new InvalidOperationException(WrongAuthInputMessage);
        }

        var requestBody = new JsonObject
        {
            ["app_key"] = configuration.AppKey,
            ["app_secret"] = configuration.AppSecret,
            ["auth_code"] = authCode,
            ["grant_type"] = "authorized_code"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildTokenExchangeUri(configuration))
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await SendForJsonAsync(request, cancellationToken);
        var data = response["data"] as JsonObject ?? response;

        var accessToken = ExtractString(data, "access_token");
        var refreshToken = ExtractString(data, "refresh_token");

        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new InvalidOperationException(MissingReturnedTokensMessage);
        }

        await _stateStore.UpdateTokensAsync(
            accessToken,
            refreshToken,
            ParseExpiry(data["access_token_expire_in"]),
            ParseExpiry(data["refresh_token_expire_in"]),
            cancellationToken);

        _logger.LogInformation(ExchangeTokenSuccessMessage);
        return data;
    }

    public async Task<JsonObject?> RefreshAccessTokenAsync(CancellationToken cancellationToken)
    {
        var configuration = await GetResolvedConfigurationAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(configuration.RefreshToken))
        {
            return null;
        }

        EnsureAppCredentials(configuration);

        var requestBody = new JsonObject
        {
            ["app_key"] = configuration.AppKey,
            ["app_secret"] = configuration.AppSecret,
            ["refresh_token"] = configuration.RefreshToken,
            ["grant_type"] = "refresh_token"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildRefreshUri(configuration))
        {
            Content = JsonContent.Create(requestBody)
        };

        var response = await SendForJsonAsync(request, cancellationToken);
        var data = response["data"] as JsonObject ?? response;

        var accessToken = ExtractString(data, "access_token");
        var nextRefreshToken = ExtractString(data, "refresh_token");

        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new InvalidOperationException(MissingRefreshResultMessage);
        }

        await _stateStore.UpdateTokensAsync(
            accessToken,
            FirstNonEmpty(nextRefreshToken, configuration.RefreshToken),
            ParseExpiry(data["access_token_expire_in"]),
            ParseExpiry(data["refresh_token_expire_in"]),
            cancellationToken);

        _logger.LogInformation(RefreshTokenSuccessMessage);
        return data;
    }

    private async Task<JsonObject> SendBusinessRequestAsync(
        ResolvedTikTokConfiguration configuration,
        HttpMethod method,
        string path,
        IReadOnlyDictionary<string, string?> queryParameters,
        JsonObject? body,
        CancellationToken cancellationToken)
    {
        var bodyJson = body?.ToJsonString(SerializerOptions);
        var requestUri = _signer.BuildSignedUri(
            configuration.ApiBaseUrl,
            path,
            configuration.AppKey,
            configuration.AppSecret,
            queryParameters,
            bodyJson);

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.TryAddWithoutValidation("x-tts-access-token", configuration.AccessToken);
        request.Headers.TryAddWithoutValidation("content-type", "application/json");

        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
        }

        return await SendForJsonAsync(request, cancellationToken);
    }

    private async Task<JsonObject> GetAuthorizedShopsAsync(
        ResolvedTikTokConfiguration configuration,
        CancellationToken cancellationToken)
    {
        return await SendBusinessRequestAsync(
            configuration,
            HttpMethod.Get,
            "/authorization/202309/shops",
            queryParameters: new Dictionary<string, string?>(StringComparer.Ordinal),
            body: null,
            cancellationToken);
    }

    private async Task<string> ResolveShopCipherAsync(
        ResolvedTikTokConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var identifier = configuration.ShopIdentifier.Trim();
        if (LooksLikeShopCipher(identifier))
        {
            return identifier;
        }

        JsonObject response;
        try
        {
            response = await GetAuthorizedShopsAsync(configuration, cancellationToken);
        }
        catch (InvalidOperationException ex) when (IsAuthorizedShopsScopeDenied(ex))
        {
            throw new InvalidOperationException(ShopCipherPermissionRequiredMessage);
        }

        var shops = (response["data"] as JsonObject)?["shops"] as JsonArray;
        if (shops is null || shops.Count == 0)
        {
            throw new InvalidOperationException(NoAuthorizedShopsMessage);
        }

        JsonObject? matchedShop = null;
        if (!string.IsNullOrWhiteSpace(identifier))
        {
            matchedShop = shops
                .OfType<JsonObject>()
                .FirstOrDefault(shop => MatchesShopIdentifier(shop, identifier));
        }
        else if (shops.Count == 1)
        {
            matchedShop = shops[0] as JsonObject;
        }
        else
        {
            throw new InvalidOperationException(MissingShopIdentifierMessage);
        }

        var shopCipher = ExtractString(matchedShop, "cipher");
        if (string.IsNullOrWhiteSpace(shopCipher))
        {
            throw new InvalidOperationException(ShopCipherResolutionFailedMessage);
        }

        return shopCipher;
    }

    private static bool MatchesShopIdentifier(JsonObject shop, string identifier) =>
        string.Equals(ExtractString(shop, "cipher"), identifier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ExtractString(shop, "id"), identifier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ExtractString(shop, "code"), identifier, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(ExtractString(shop, "name"), identifier, StringComparison.OrdinalIgnoreCase);

    private static bool IsAuthorizedShopsScopeDenied(InvalidOperationException exception) =>
        exception.Message.Contains("\"code\":105005", StringComparison.Ordinal) ||
        exception.Message.Contains("code 105005", StringComparison.Ordinal) ||
        (exception.Message.Contains("Access denied", StringComparison.OrdinalIgnoreCase) &&
         exception.Message.Contains("access scopes granted", StringComparison.OrdinalIgnoreCase));

    private async Task<JsonObject> SendForJsonAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await SendWithRetryAsync(request, cancellationToken);
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

    private async Task<HttpResponseMessage> SendWithRetryAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Exception? lastException = null;

        for (var attempt = 1; attempt <= MaxSendAttempts; attempt++)
        {
            using var clonedRequest = await CloneHttpRequestMessageAsync(request, cancellationToken);

            try
            {
                return await _httpClientFactory.CreateClient("TikTokApi").SendAsync(clonedRequest, cancellationToken);
            }
            catch (Exception ex) when (IsTransientSendFailure(ex, cancellationToken))
            {
                lastException = ex;

                if (attempt == MaxSendAttempts)
                {
                    break;
                }

                _logger.LogWarning(
                    ex,
                    "TikTok API request send failed on attempt {Attempt}/{MaxAttempts}. Retrying.",
                    attempt,
                    MaxSendAttempts);

                await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException(BuildHttpSendFailureMessage(lastException), lastException);
    }

    private static bool IsTransientSendFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is HttpRequestException ||
        (exception is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    private static string BuildHttpSendFailureMessage(Exception? exception)
    {
        var details = new List<string>
        {
            "TikTok API network request failed after retries"
        };

        if (!string.IsNullOrWhiteSpace(exception?.Message))
        {
            details.Add(exception.Message);
        }

        if (!string.IsNullOrWhiteSpace(exception?.InnerException?.Message))
        {
            details.Add(exception.InnerException.Message);
        }

        return string.Join(". ", details);
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var contentBytes = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            var contentClone = new ByteArrayContent(contentBytes);

            foreach (var header in request.Content.Headers)
            {
                contentClone.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = contentClone;
        }

        return clone;
    }

    private async Task<ResolvedTikTokConfiguration> GetResolvedConfigurationAsync(CancellationToken cancellationToken)
    {
        var options = _options.Value;
        var state = await _stateStore.GetSnapshotAsync(cancellationToken);

        return new ResolvedTikTokConfiguration(
            options.ApiBaseUrl,
            options.AuthBaseUrl,
            options.TokenExchangePath,
            options.TokenRefreshPath,
            FirstNonEmpty(state.AppKey, options.AppKey),
            FirstNonEmpty(state.AppSecret, options.AppSecret),
            FirstNonEmpty(state.AccessToken, options.AccessToken),
            FirstNonEmpty(state.RefreshToken, options.RefreshToken),
            FirstNonEmpty(state.ShopId, options.ShopId));
    }

    private static void EnsureAppCredentials(ResolvedTikTokConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.AppKey) || string.IsNullOrWhiteSpace(configuration.AppSecret))
        {
            throw new InvalidOperationException(MissingAppCredentialsMessage);
        }
    }

    private static void EnsureAccessToken(ResolvedTikTokConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.AccessToken))
        {
            throw new InvalidOperationException(MissingAccessTokenMessage);
        }
    }

    private static Uri BuildTokenExchangeUri(ResolvedTikTokConfiguration configuration) =>
        BuildAuthUri(configuration.AuthBaseUrl, configuration.TokenExchangePath);

    private static Uri BuildRefreshUri(ResolvedTikTokConfiguration configuration) =>
        BuildAuthUri(configuration.AuthBaseUrl, configuration.TokenRefreshPath);

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

    private static string NormalizeAuthorizationCode(string authCodeOrUrl)
    {
        var value = authCodeOrUrl.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (value.Contains("code=", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractValueAfterMarker(value, "code=");
        }

        if (value.Contains("code:", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractValueAfterMarker(value, "code:");
        }

        return WebUtility.UrlDecode(value);
    }

    private static string ExtractValueAfterMarker(string input, string marker)
    {
        var start = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += marker.Length;
        while (start < input.Length && char.IsWhiteSpace(input[start]))
        {
            start++;
        }

        var end = start;
        while (end < input.Length &&
               input[end] != '&' &&
               input[end] != '\r' &&
               input[end] != '\n' &&
               !char.IsWhiteSpace(input[end]))
        {
            end++;
        }

        return WebUtility.UrlDecode(input[start..end].Trim());
    }

    private static bool LooksLikeAccessToken(string value) =>
        value.StartsWith("ROW_", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeShopCipher(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains('_');

    private static string FirstNonEmpty(string? first, string second) =>
        string.IsNullOrWhiteSpace(first) ? second : first;

    private static string? ExtractString(JsonObject? node, string propertyName) =>
        node?[propertyName]?.GetValue<string?>() ?? node?[propertyName]?.ToString();

    private static DateTimeOffset? ParseExpiry(JsonNode? node)
    {
        if (node is not JsonValue valueNode)
        {
            return null;
        }

        if (valueNode.TryGetValue<long>(out var longValue))
        {
            return NormalizeExpiry(longValue);
        }

        if (long.TryParse(valueNode.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return NormalizeExpiry(parsed);
        }

        return null;
    }

    private static DateTimeOffset NormalizeExpiry(long value)
    {
        var nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return value > nowUnix + 3600
            ? DateTimeOffset.FromUnixTimeSeconds(value)
            : DateTimeOffset.UtcNow.AddSeconds(value);
    }

    private sealed record ResolvedTikTokConfiguration(
        string ApiBaseUrl,
        string AuthBaseUrl,
        string TokenExchangePath,
        string TokenRefreshPath,
        string AppKey,
        string AppSecret,
        string AccessToken,
        string RefreshToken,
        string ShopIdentifier);
}

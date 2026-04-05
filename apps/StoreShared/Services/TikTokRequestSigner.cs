using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TikTokOrderPrinter.Services;

public sealed class TikTokRequestSigner
{
    public Uri BuildSignedUri(
        string apiBaseUrl,
        string path,
        string appKey,
        string appSecret,
        IReadOnlyDictionary<string, string?> queryParameters,
        string? bodyJson)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signableParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["app_key"] = appKey,
            ["timestamp"] = timestamp
        };

        foreach (var parameter in queryParameters)
        {
            if (string.IsNullOrWhiteSpace(parameter.Value) ||
                string.Equals(parameter.Key, "sign", StringComparison.Ordinal) ||
                string.Equals(parameter.Key, "access_token", StringComparison.Ordinal))
            {
                continue;
            }

            signableParameters[parameter.Key] = parameter.Value!;
        }

        var signature = ComputeSignature(path, signableParameters, bodyJson, appSecret);

        var allQueryParameters = new Dictionary<string, string?>(queryParameters, StringComparer.Ordinal)
        {
            ["app_key"] = appKey,
            ["sign"] = signature,
            ["timestamp"] = timestamp
        };

        return new Uri(new Uri(EnsureTrailingSlash(apiBaseUrl)), $"{path.TrimStart('/')}?{BuildQueryString(allQueryParameters)}");
    }

    private static string ComputeSignature(
        string path,
        SortedDictionary<string, string> parameters,
        string? bodyJson,
        string appSecret)
    {
        var payloadBuilder = new StringBuilder()
            .Append(appSecret)
            .Append(path);

        foreach (var parameter in parameters)
        {
            payloadBuilder.Append(parameter.Key).Append(parameter.Value);
        }

        if (!string.IsNullOrWhiteSpace(bodyJson))
        {
            payloadBuilder.Append(bodyJson);
        }

        payloadBuilder.Append(appSecret);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(appSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadBuilder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildQueryString(IEnumerable<KeyValuePair<string, string?>> values) =>
        string.Join("&", values
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}"));

    private static string EnsureTrailingSlash(string value) =>
        value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
}

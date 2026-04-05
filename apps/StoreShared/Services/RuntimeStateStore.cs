using System.Text.Json;
using Microsoft.Extensions.Options;
using TikTokOrderPrinter.Models;
using TikTokOrderPrinter.Options;

namespace TikTokOrderPrinter.Services;

public sealed class RuntimeStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    private readonly SemaphoreSlim _mutex = new(1, 1);
    private readonly string _stateFilePath;
    private RuntimeState _state = new();
    private bool _loaded;

    public RuntimeStateStore(IOptions<AppDataOptions> options)
    {
        _stateFilePath = Path.GetFullPath(options.Value.StateFilePath);
    }

    public async Task<RuntimeState> GetSnapshotAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            return Clone(_state);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<LocalAppConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            return CloneConfiguration(_state);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task SaveConfigurationAsync(LocalAppConfiguration configuration, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);

            var nextAccessToken = Normalize(configuration.AccessToken);
            var nextRefreshToken = Normalize(configuration.RefreshToken);
            var tokensChanged = !string.Equals(_state.AccessToken, nextAccessToken, StringComparison.Ordinal)
                                || !string.Equals(_state.RefreshToken, nextRefreshToken, StringComparison.Ordinal);

            _state.StoreName = Normalize(configuration.StoreName);
            _state.AppKey = Normalize(configuration.AppKey);
            _state.AppSecret = Normalize(configuration.AppSecret);
            _state.AccessToken = nextAccessToken;
            _state.RefreshToken = nextRefreshToken;
            _state.ShopId = Normalize(configuration.ShopId);
            _state.PrinterName = Normalize(configuration.PrinterName);
            _state.PaperSize = Normalize(configuration.PaperSize);
            _state.CustomPaperWidthMm = Normalize(configuration.CustomPaperWidthMm);
            _state.CustomPaperHeightMm = Normalize(configuration.CustomPaperHeightMm);
            _state.MarginMm = Normalize(configuration.MarginMm);
            _state.PaperWidthCharacters = Normalize(configuration.PaperWidthCharacters);
            _state.BaseFontSize = Normalize(configuration.BaseFontSize);
            _state.MinFontSize = Normalize(configuration.MinFontSize);
            _state.AutoPrintNewOrders = configuration.AutoPrintNewOrders;
            _state.AutoPrintAfterBridgeCapture = configuration.AutoPrintAfterBridgeCapture;
            _state.ShowBuyerAccountName = configuration.ShowBuyerAccountName;
            _state.ShowBuyerPlatformUserId = configuration.ShowBuyerPlatformUserId;
            _state.ShowBuyerName = configuration.ShowBuyerName;
            _state.ShowBuyerEmail = configuration.ShowBuyerEmail;
            _state.ShowRecipientPhone = configuration.ShowRecipientPhone;
            _state.ShowBuyerMessage = configuration.ShowBuyerMessage;
            _state.ShowOrderAmounts = configuration.ShowOrderAmounts;
            _state.ShowItemDetails = configuration.ShowItemDetails;
            _state.ShowSku = configuration.ShowSku;
            _state.ShowPaidTime = configuration.ShowPaidTime;
            _state.ShowCreatedTime = configuration.ShowCreatedTime;
            _state.SelectedRawFieldPaths = Normalize(configuration.SelectedRawFieldPaths);

            if (tokensChanged)
            {
                _state.AccessTokenExpiresAtUtc = null;
                _state.RefreshTokenExpiresAtUtc = null;
            }

            await SaveLockedAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> WasProcessedAsync(string orderId, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            return _state.ProcessedOrders.Any(x => string.Equals(x.OrderId, orderId, StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task MarkProcessedAsync(PrintedOrderRecord record, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);

            var existing = _state.ProcessedOrders.FirstOrDefault(x =>
                string.Equals(x.OrderId, record.OrderId, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                _state.ProcessedOrders.Add(record);
            }
            else
            {
                existing.DisplayName = record.DisplayName;
                existing.BuyerAccountName = MergeString(record.BuyerAccountName, existing.BuyerAccountName);
                existing.BuyerAccountNameSource = MergeString(record.BuyerAccountNameSource, existing.BuyerAccountNameSource);
                existing.BuyerAccountNameSourceUrl = MergeString(record.BuyerAccountNameSourceUrl, existing.BuyerAccountNameSourceUrl);
                existing.BuyerAccountNameCapturedAtUtc = record.BuyerAccountNameCapturedAtUtc ?? existing.BuyerAccountNameCapturedAtUtc;
                existing.BuyerPlatformUserId = MergeString(record.BuyerPlatformUserId, existing.BuyerPlatformUserId);
                existing.BuyerName = MergeString(record.BuyerName, existing.BuyerName);
                existing.BuyerEmail = MergeString(record.BuyerEmail, existing.BuyerEmail);
                existing.RecipientName = MergeString(record.RecipientName, existing.RecipientName);
                existing.RecipientPhone = MergeString(record.RecipientPhone, existing.RecipientPhone);
                existing.RecipientAddress = MergeString(record.RecipientAddress, existing.RecipientAddress);
                existing.Status = MergeString(record.Status, existing.Status);
                existing.TotalAmount = record.TotalAmount ?? existing.TotalAmount;
                existing.Currency = MergeString(record.Currency, existing.Currency);
                existing.CreatedAtUtc = record.CreatedAtUtc ?? existing.CreatedAtUtc;
                existing.UpdatedAtUtc = record.UpdatedAtUtc ?? existing.UpdatedAtUtc;
                existing.PaidAtUtc = record.PaidAtUtc ?? existing.PaidAtUtc;
                existing.ProcessedAtUtc = record.ProcessedAtUtc;
                existing.PrintedAtUtc = record.PrintedAtUtc ?? existing.PrintedAtUtc;
                existing.PrintCount = record.PrintCount;
                existing.TicketFilePath = MergeString(record.TicketFilePath, existing.TicketFilePath);
                existing.PayloadFilePath = MergeString(record.PayloadFilePath, existing.PayloadFilePath);
                existing.TicketContent = MergeString(record.TicketContent, existing.TicketContent);
                existing.PrintError = record.PrintError;
            }

            await SaveLockedAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<List<PrintedOrderRecord>> GetRecentOrdersAsync(int count, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            return _state.ProcessedOrders
                .OrderByDescending(x => x.PaidAtUtc ?? x.CreatedAtUtc ?? x.ProcessedAtUtc)
                .ThenByDescending(x => x.ProcessedAtUtc)
                .Take(count)
                .Select(Clone)
                .ToList();
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<PrintedOrderRecord?> GetOrderAsync(string orderId, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            var record = _state.ProcessedOrders.FirstOrDefault(x =>
                string.Equals(x.OrderId, orderId, StringComparison.OrdinalIgnoreCase));

            return record is null ? null : Clone(record);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<PrintedOrderRecord> MergeSellerCenterCaptureAsync(
        string orderId,
        string buyerNickname,
        string buyerName,
        string sourceUrl,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);

            var normalizedOrderId = Normalize(orderId);
            var normalizedNickname = Normalize(buyerNickname);
            var normalizedBuyerName = Normalize(buyerName);
            var normalizedSourceUrl = Normalize(sourceUrl);
            var capturedAtUtc = DateTimeOffset.UtcNow;

            var record = _state.ProcessedOrders.FirstOrDefault(x =>
                string.Equals(x.OrderId, normalizedOrderId, StringComparison.OrdinalIgnoreCase));

            if (record is null)
            {
                return new PrintedOrderRecord
                {
                    OrderId = normalizedOrderId,
                    BuyerAccountName = normalizedNickname,
                    BuyerAccountNameSource = "seller_center_bridge",
                    BuyerAccountNameSourceUrl = normalizedSourceUrl,
                    BuyerAccountNameCapturedAtUtc = capturedAtUtc,
                    BuyerName = normalizedBuyerName,
                    ProcessedAtUtc = capturedAtUtc
                };
            }

            if (!string.IsNullOrWhiteSpace(normalizedNickname))
            {
                if (!string.IsNullOrWhiteSpace(record.BuyerAccountName) &&
                    !string.Equals(record.BuyerAccountName, normalizedNickname, StringComparison.OrdinalIgnoreCase))
                {
                    return Clone(record);
                }

                record.BuyerAccountName = normalizedNickname;
                record.BuyerAccountNameSource = "seller_center_bridge";
                record.BuyerAccountNameSourceUrl = normalizedSourceUrl;
                record.BuyerAccountNameCapturedAtUtc = capturedAtUtc;
            }

            if (!string.IsNullOrWhiteSpace(normalizedBuyerName))
            {
                record.BuyerName = normalizedBuyerName;
            }

            await SaveLockedAsync(cancellationToken);
            return Clone(record);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdatePrintResultAsync(string orderId, DateTimeOffset? printedAtUtc, string printError, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);

            var record = _state.ProcessedOrders.FirstOrDefault(x =>
                string.Equals(x.OrderId, orderId, StringComparison.OrdinalIgnoreCase));

            if (record is not null)
            {
                record.PrintedAtUtc = printedAtUtc;
                record.PrintError = printError;
                if (printedAtUtc is not null)
                {
                    record.PrintCount++;
                }

                await SaveLockedAsync(cancellationToken);
            }
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdateTokensAsync(
        string accessToken,
        string refreshToken,
        DateTimeOffset? accessTokenExpiresAtUtc,
        DateTimeOffset? refreshTokenExpiresAtUtc,
        CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            _state.AccessToken = Normalize(accessToken);
            _state.RefreshToken = Normalize(refreshToken);
            _state.AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
            _state.RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
            await SaveLockedAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task UpdateLastPollCompletedAsync(DateTimeOffset completedAtUtc, CancellationToken cancellationToken)
    {
        await _mutex.WaitAsync(cancellationToken);
        try
        {
            await EnsureLoadedLockedAsync(cancellationToken);
            _state.LastPollCompletedAtUtc = completedAtUtc;
            await SaveLockedAsync(cancellationToken);
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task EnsureLoadedLockedAsync(CancellationToken cancellationToken)
    {
        if (_loaded)
        {
            return;
        }

        var directoryPath = Path.GetDirectoryName(_stateFilePath);
        if (!string.IsNullOrWhiteSpace(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        if (File.Exists(_stateFilePath))
        {
            var json = await File.ReadAllTextAsync(_stateFilePath, cancellationToken);
            _state = JsonSerializer.Deserialize<RuntimeState>(json, SerializerOptions) ?? new RuntimeState();
        }

        _loaded = true;
    }

    private async Task SaveLockedAsync(CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(_state, SerializerOptions);
        await File.WriteAllTextAsync(_stateFilePath, json, cancellationToken);
    }

    private static RuntimeState Clone(RuntimeState state) =>
        new()
        {
            StoreName = state.StoreName,
            AppKey = state.AppKey,
            AppSecret = state.AppSecret,
            AccessToken = state.AccessToken,
            RefreshToken = state.RefreshToken,
            ShopId = state.ShopId,
            PrinterName = state.PrinterName,
            PaperSize = state.PaperSize,
            CustomPaperWidthMm = state.CustomPaperWidthMm,
            CustomPaperHeightMm = state.CustomPaperHeightMm,
            MarginMm = state.MarginMm,
            PaperWidthCharacters = state.PaperWidthCharacters,
            BaseFontSize = state.BaseFontSize,
            MinFontSize = state.MinFontSize,
            AutoPrintNewOrders = state.AutoPrintNewOrders,
            AutoPrintAfterBridgeCapture = state.AutoPrintAfterBridgeCapture,
            ShowBuyerAccountName = state.ShowBuyerAccountName,
            ShowBuyerPlatformUserId = state.ShowBuyerPlatformUserId,
            ShowBuyerName = state.ShowBuyerName,
            ShowBuyerEmail = state.ShowBuyerEmail,
            ShowRecipientPhone = state.ShowRecipientPhone,
            ShowBuyerMessage = state.ShowBuyerMessage,
            ShowOrderAmounts = state.ShowOrderAmounts,
            ShowItemDetails = state.ShowItemDetails,
            ShowSku = state.ShowSku,
            ShowPaidTime = state.ShowPaidTime,
            ShowCreatedTime = state.ShowCreatedTime,
            SelectedRawFieldPaths = [.. state.SelectedRawFieldPaths],
            AccessTokenExpiresAtUtc = state.AccessTokenExpiresAtUtc,
            RefreshTokenExpiresAtUtc = state.RefreshTokenExpiresAtUtc,
            LastPollCompletedAtUtc = state.LastPollCompletedAtUtc,
            ProcessedOrders = state.ProcessedOrders.Select(Clone).ToList()
        };

    private static LocalAppConfiguration CloneConfiguration(RuntimeState state) =>
        new()
        {
            StoreName = state.StoreName,
            AppKey = state.AppKey,
            AppSecret = state.AppSecret,
            AccessToken = state.AccessToken,
            RefreshToken = state.RefreshToken,
            ShopId = state.ShopId,
            PrinterName = state.PrinterName,
            PaperSize = state.PaperSize,
            CustomPaperWidthMm = state.CustomPaperWidthMm,
            CustomPaperHeightMm = state.CustomPaperHeightMm,
            MarginMm = state.MarginMm,
            PaperWidthCharacters = state.PaperWidthCharacters,
            BaseFontSize = state.BaseFontSize,
            MinFontSize = state.MinFontSize,
            AutoPrintNewOrders = state.AutoPrintNewOrders,
            AutoPrintAfterBridgeCapture = state.AutoPrintAfterBridgeCapture,
            ShowBuyerAccountName = state.ShowBuyerAccountName,
            ShowBuyerPlatformUserId = state.ShowBuyerPlatformUserId,
            ShowBuyerName = state.ShowBuyerName,
            ShowBuyerEmail = state.ShowBuyerEmail,
            ShowRecipientPhone = state.ShowRecipientPhone,
            ShowBuyerMessage = state.ShowBuyerMessage,
            ShowOrderAmounts = state.ShowOrderAmounts,
            ShowItemDetails = state.ShowItemDetails,
            ShowSku = state.ShowSku,
            ShowPaidTime = state.ShowPaidTime,
            ShowCreatedTime = state.ShowCreatedTime,
            SelectedRawFieldPaths = [.. state.SelectedRawFieldPaths]
        };

    private static PrintedOrderRecord Clone(PrintedOrderRecord record) =>
        new()
        {
            OrderId = record.OrderId,
            DisplayName = record.DisplayName,
            BuyerAccountName = record.BuyerAccountName,
            BuyerAccountNameSource = record.BuyerAccountNameSource,
            BuyerAccountNameSourceUrl = record.BuyerAccountNameSourceUrl,
            BuyerAccountNameCapturedAtUtc = record.BuyerAccountNameCapturedAtUtc,
            BuyerPlatformUserId = record.BuyerPlatformUserId,
            BuyerName = record.BuyerName,
            BuyerEmail = record.BuyerEmail,
            RecipientName = record.RecipientName,
            RecipientPhone = record.RecipientPhone,
            RecipientAddress = record.RecipientAddress,
            Status = record.Status,
            TotalAmount = record.TotalAmount,
            Currency = record.Currency,
            CreatedAtUtc = record.CreatedAtUtc,
            UpdatedAtUtc = record.UpdatedAtUtc,
            PaidAtUtc = record.PaidAtUtc,
            ProcessedAtUtc = record.ProcessedAtUtc,
            PrintedAtUtc = record.PrintedAtUtc,
            PrintCount = record.PrintCount,
            TicketFilePath = record.TicketFilePath,
            PayloadFilePath = record.PayloadFilePath,
            TicketContent = record.TicketContent,
            PrintError = record.PrintError
        };

    private static string Normalize(string? value) => value?.Trim() ?? string.Empty;

    private static string MergeString(string? incoming, string existing) =>
        string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();

    private static double? Normalize(double? value) => value is > 0d ? Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;

    private static int? Normalize(int? value) => value is > 0 ? value : null;

    private static float? Normalize(float? value) => value is > 0f ? (float)Math.Round(value.Value, 2, MidpointRounding.AwayFromZero) : null;

    private static List<string> Normalize(IEnumerable<string>? values) =>
        values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList()
        ?? [];
}

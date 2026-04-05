# TikTok Order Printer

This project is a local Windows service that does three things:

1. Polls TikTok Shop for recent orders.
2. Deduplicates orders and saves a ticket plus the raw JSON payload.
3. Sends the ticket to the local Windows printer.

## Configure

Edit `appsettings.json` and set:

- `TikTokShop:AppKey`
- `TikTokShop:AppSecret`
- `TikTokShop:AccessToken`
- `TikTokShop:RefreshToken`
- `TikTokShop:ShopId`

If your token refresh endpoint uses a different host, update:

- `TikTokShop:AuthBaseUrl`
- `TikTokShop:TokenRefreshPath`

## Run

```powershell
dotnet restore --configfile .\NuGet.Config
dotnet run
```

Then open the local panel at the address shown in the terminal.

## Files

- Runtime state: `Data/runtime-state.json`
- Tickets and payloads: `Data/print-jobs`

## Current implementation notes

- Order search, order detail, and token refresh paths are based on the TikTok Shop public Postman workspace.
- Signature generation is inferred from a public TikTok Shop SDK implementation:
  - sort query parameters alphabetically
  - exclude `sign` and `access_token`
  - concatenate `path + app_secret + sortedParams + body + app_secret`
  - hash with `HMAC-SHA256`
- The first version uses polling plus local deduplication and does not require a public webhook.
- If printing fails, the ticket is still saved locally and can be reprinted from the dashboard.

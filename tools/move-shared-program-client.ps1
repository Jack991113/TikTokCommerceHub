$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$shared = Join-Path $repoRoot "apps\StoreShared"
$store1 = Join-Path $repoRoot "apps\Store1"
$store2 = Join-Path $repoRoot "apps\Store2"

Copy-Item -LiteralPath (Join-Path $store1 "Program.cs") -Destination (Join-Path $shared "Program.cs") -Force
Copy-Item -LiteralPath (Join-Path $store1 "Services\TikTokShopClient.cs") -Destination (Join-Path $shared "Services\TikTokShopClient.cs") -Force

Remove-Item -LiteralPath (Join-Path $store1 "Program.cs") -Force
Remove-Item -LiteralPath (Join-Path $store2 "Program.cs") -Force
Remove-Item -LiteralPath (Join-Path $store1 "Services\TikTokShopClient.cs") -Force
Remove-Item -LiteralPath (Join-Path $store2 "Services\TikTokShopClient.cs") -Force

Write-Host "Moved Program.cs and TikTokShopClient.cs to StoreShared"

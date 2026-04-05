$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$shared = Join-Path $repoRoot "apps\StoreShared"
$store1 = Join-Path $repoRoot "apps\Store1"
$store2 = Join-Path $repoRoot "apps\Store2"

$files = @(
  "appsettings.Development.json",
  "appsettings.json",
  "bridge-check.user.js",
  "Models\ApiFieldOption.cs",
  "Models\AuthCodeExchangeRequest.cs",
  "Models\BatchPrintRequest.cs",
  "Models\LocalAppConfiguration.cs",
  "Models\OrderItemPrintModel.cs",
  "Models\OrderListItem.cs",
  "Models\OrderPrintArtifacts.cs",
  "Models\OrderPrintModel.cs",
  "Models\OrderQueryRequest.cs",
  "Models\OrderQueryResponse.cs",
  "Models\PollRunResult.cs",
  "Models\PrintedOrderRecord.cs",
  "Models\RuntimeState.cs",
  "Models\SellerCenterCaptureRequest.cs",
  "Models\ServiceStatusSnapshot.cs",
  "Options\AppDataOptions.cs",
  "Options\PrintingOptions.cs",
  "Options\TikTokShopOptions.cs",
  "Services\LabelPaperProfiles.cs",
  "Services\OrderItemGrouping.cs",
  "Services\OrderPayloadMapper.cs",
  "Services\OrderPollingWorker.cs",
  "Services\OrderPrintEligibility.cs",
  "Services\OrderTicketRenderer.cs",
  "Services\RuntimeStateStore.cs",
  "Services\SellerCenterBridgeScriptBuilder.cs",
  "Services\ServiceStatusStore.cs",
  "Services\TikTokApiFieldCatalog.cs",
  "Services\TikTokRequestSigner.cs",
  "Services\WindowsPrintService.cs",
  "wwwroot\manifest.json",
  "wwwroot\sw.js"
)

foreach ($relativePath in $files) {
  $sharedTarget = Join-Path $shared $relativePath
  $sharedDir = Split-Path $sharedTarget -Parent
  if (-not (Test-Path $sharedDir)) {
    New-Item -ItemType Directory -Path $sharedDir -Force | Out-Null
  }

  Copy-Item -LiteralPath (Join-Path $store1 $relativePath) -Destination $sharedTarget -Force
  Remove-Item -LiteralPath (Join-Path $store1 $relativePath) -Force
  Remove-Item -LiteralPath (Join-Path $store2 $relativePath) -Force
}

Write-Host "Moved shared store files to $shared"

param(
  [ValidateSet("ConfiguredOwner", "ClientClean")]
  [string]$Mode = "ClientClean",
  [string]$Runtime = "win-x64",
  [switch]$NoZip
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageName = "TikTokCommerceHub-$Mode-$timestamp"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$packageRoot = Join-Path $artifactsRoot $packageName
$publishRoot = Join-Path $packageRoot "apps"

$store1Project = Join-Path $repoRoot "apps\Store1\TikTokOrderPrinter.Store1.csproj"
$store2Project = Join-Path $repoRoot "apps\Store2\TikTokOrderPrinter.Store2.csproj"
$dashboardProject = Join-Path $repoRoot "apps\Dashboard\TikTokSalesStats.csproj"

$store1Publish = Join-Path $publishRoot "Store1"
$store2Publish = Join-Path $publishRoot "Store2"
$dashboardPublish = Join-Path $publishRoot "Dashboard"

if (Test-Path $packageRoot) {
  Remove-Item -LiteralPath $packageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $store1Publish -Force | Out-Null
New-Item -ItemType Directory -Path $store2Publish -Force | Out-Null
New-Item -ItemType Directory -Path $dashboardPublish -Force | Out-Null

$publishArgs = @(
  "-c", "Release",
  "-r", $Runtime,
  "--self-contained", "true"
)

dotnet publish $store1Project @publishArgs -o $store1Publish
dotnet publish $store2Project @publishArgs -o $store2Publish
dotnet publish $dashboardProject @publishArgs -o $dashboardPublish

function Copy-StoreData {
  param(
    [string]$SourceRoot,
    [string]$TargetRoot
  )

  $sourceData = Join-Path $SourceRoot "Data"
  $targetData = Join-Path $TargetRoot "Data"
  New-Item -ItemType Directory -Path $targetData -Force | Out-Null

  if ($Mode -eq "ConfiguredOwner") {
    if (Test-Path $sourceData) {
      Copy-Item -LiteralPath $sourceData -Destination $TargetRoot -Recurse -Force
    }
    return
  }

  $templatePath = Join-Path $sourceData "ticket-template.txt"
  if (Test-Path $templatePath) {
    Copy-Item -LiteralPath $templatePath -Destination (Join-Path $targetData "ticket-template.txt") -Force
  }
}

Copy-StoreData -SourceRoot (Join-Path $repoRoot "apps\Store1") -TargetRoot $store1Publish
Copy-StoreData -SourceRoot (Join-Path $repoRoot "apps\Store2") -TargetRoot $store2Publish

$readme = @"
TikTok Commerce Hub package

Mode: $Mode
Runtime: $Runtime

URLs
- Store1: http://localhost:5038
- Store2: http://localhost:5039
- Dashboard: http://localhost:5048

Start
- Double click: Start Commerce Suite.cmd

Stop
- Double click: Stop Commerce Suite.cmd

If you are sending this to a client, use ClientClean mode.
"@
Set-Content -LiteralPath (Join-Path $packageRoot "README.txt") -Value $readme -Encoding UTF8

$startScript = @"
@echo off
setlocal
cd /d "%~dp0"

start "CommerceHub Store1" /D "%~dp0apps\Store1" "%~dp0apps\Store1\TikTokOrderPrinter.Store1.exe" --urls http://localhost:5038
start "CommerceHub Store2" /D "%~dp0apps\Store2" "%~dp0apps\Store2\TikTokOrderPrinter.Store2.exe" --urls http://localhost:5039
start "CommerceHub Dashboard" /D "%~dp0apps\Dashboard" "%~dp0apps\Dashboard\TikTokSalesStats.exe" --urls http://localhost:5048

timeout /t 4 /nobreak >nul
start "" "http://localhost:5038"
start "" "http://localhost:5039"
start "" "http://localhost:5048"
"@
Set-Content -LiteralPath (Join-Path $packageRoot "Start Commerce Suite.cmd") -Value $startScript -Encoding ASCII

$stopScript = @"
@echo off
for %%P in (5038 5039 5048) do (
  for /f "tokens=5" %%I in ('netstat -ano ^| findstr :%%P ^| findstr LISTENING') do (
    taskkill /PID %%I /F >nul 2>nul
  )
)
"@
Set-Content -LiteralPath (Join-Path $packageRoot "Stop Commerce Suite.cmd") -Value $stopScript -Encoding ASCII

if (-not $NoZip) {
  $zipPath = "$packageRoot.zip"
  if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
  }

  Compress-Archive -Path $packageRoot -DestinationPath $zipPath -CompressionLevel Optimal
  Write-Host "Zip package created: $zipPath"
}

Write-Host "Package created at: $packageRoot"

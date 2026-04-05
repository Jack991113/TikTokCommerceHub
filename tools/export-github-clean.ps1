param(
  [string]$ExportRoot = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path $PSScriptRoot -Parent
if ([string]::IsNullOrWhiteSpace($ExportRoot)) {
  $ExportRoot = Join-Path (Split-Path $repoRoot -Parent) ("TikTokCommerceHub-GitHub-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
}

if (Test-Path $ExportRoot) {
  Remove-Item -LiteralPath $ExportRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $ExportRoot -Force | Out-Null

$sourceFiles = Get-ChildItem -Path $repoRoot -Recurse -File | Where-Object {
  $_.FullName -notmatch '\\\.git\\' -and
  $_.FullName -notmatch '\\bin\\' -and
  $_.FullName -notmatch '\\obj\\' -and
  $_.FullName -notmatch '\\\.build\\' -and
  $_.FullName -notmatch '\\\.verify\\' -and
  $_.FullName -notmatch '\\\.verify2\\' -and
  $_.FullName -notmatch '\\artifacts\\' -and
  $_.FullName -notmatch '\\logs\\' -and
  $_.FullName -notmatch '\\Data\\print-jobs\\' -and
  $_.Name -ne "runtime-state.json"
}

foreach ($file in $sourceFiles) {
  $relativePath = $file.FullName.Substring($repoRoot.Length + 1)
  $destinationPath = Join-Path $ExportRoot $relativePath
  $destinationDir = Split-Path $destinationPath -Parent
  if (-not (Test-Path $destinationDir)) {
    New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
  }

  Copy-Item -LiteralPath $file.FullName -Destination $destinationPath -Force
}

$dataDirs = @(
  "apps\Store1\Data",
  "apps\Store1\Data\print-jobs",
  "apps\Store2\Data",
  "apps\Store2\Data\print-jobs",
  "apps\Dashboard\Data"
)

foreach ($relativeDir in $dataDirs) {
  $dir = Join-Path $ExportRoot $relativeDir
  New-Item -ItemType Directory -Path $dir -Force | Out-Null
  Set-Content -LiteralPath (Join-Path $dir ".gitkeep") -Value "" -Encoding ASCII
}

$dashboardConfigPath = Join-Path $ExportRoot "apps\Dashboard\appsettings.json"
$dashboardJson = Get-Content $dashboardConfigPath -Raw | ConvertFrom-Json
$dashboardJson.SalesStats.Stores[0].RuntimeStatePath = "..\\Store1\\Data\\runtime-state.json"
$dashboardJson.SalesStats.Stores[1].RuntimeStatePath = "..\\Store2\\Data\\runtime-state.json"
$dashboardJson | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $dashboardConfigPath -Encoding UTF8

$guide = @"
GitHub upload steps
1. Sign in to https://github.com in your browser.
2. Click the + button in the top-right corner, then choose New repository.
3. Repository name: TikTokCommerceHub
4. Do not add README, .gitignore, or license on GitHub.
5. Click Create repository.
6. Copy the HTTPS repository URL, for example:
   https://github.com/your-name/TikTokCommerceHub.git
7. In this folder, double-click:
   INIT_AND_PUSH_TO_GITHUB.cmd
8. Paste the repository URL when the script asks for it.

If GitHub asks you to sign in during push:
- Follow the browser sign-in flow.
- If Git asks for credentials, prefer a GitHub token instead of your password.
"@
Set-Content -LiteralPath (Join-Path $ExportRoot "GITHUB_UPLOAD_GUIDE.txt") -Value $guide -Encoding UTF8

$pushScript = @"
@echo off
setlocal
cd /d "%~dp0"

set /p REMOTE_URL=Please paste your GitHub repository URL (https://github.com/your-name/repo.git): 
if "%REMOTE_URL%"=="" (
  echo No repository URL entered. Cancelled.
  pause
  exit /b 1
)

git init
git branch -M main
git add .
git commit -m "Initial clean source export"
git remote remove origin >nul 2>nul
git remote add origin %REMOTE_URL%
git push -u origin main

echo.
echo If push succeeded, the clean source has been uploaded to GitHub.
pause
"@
Set-Content -LiteralPath (Join-Path $ExportRoot "INIT_AND_PUSH_TO_GITHUB.cmd") -Value $pushScript -Encoding ASCII

Write-Host "GitHub clean export created at: $ExportRoot"

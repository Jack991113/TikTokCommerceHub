# TikTokCommerceHub

TikTokCommerceHub is the cleaned-up source tree for the TikTok order printing, bridge capture, and analytics suite.

This repository is organized so the currently running business apps can keep working, while the codebase is easier to maintain, package, and publish to GitHub.

## What is included

- `apps/Store1`
  - Store 1 auto-print app
- `apps/Store2`
  - Store 2 auto-print app
- `apps/Dashboard`
  - Sales, reconciliation, product, and streamer analytics dashboard
- `apps/StoreShared`
  - Shared store logic used by Store 1 and Store 2
- `src/`
  - New layered architecture foundation (`Domain / Application / Infrastructure / Web`)
- `tools/`
  - Packaging and export scripts
- `docs/`
  - Architecture and usage documents

## Main features

- Two-store local auto-print workflow
- TikTok Seller Center bridge capture for buyer handle
- Ticket template printing
- Product performance analytics
- Streamer compensation analytics
- Reconciliation and payout views
- Clean packaging for owner deployment and client deployment

## Default local ports

- Store 1: `http://localhost:5038`
- Store 2: `http://localhost:5039`
- Dashboard: `http://localhost:5048`

## How to start

### Option 1: start the full suite

Use the generated launcher:

- `Start Commerce Suite.cmd`

### Option 2: start projects manually

Store 1:

```powershell
cd apps\Store1
dotnet run
```

Store 2:

```powershell
cd apps\Store2
dotnet run
```

Dashboard:

```powershell
cd apps\Dashboard
dotnet run
```

## How to configure

### Store apps

The store apps read runtime configuration from:

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`

Typical fields include:

- TikTok app key
- TikTok app secret
- access token
- refresh token
- shop id or shop cipher
- printer name
- paper size
- print options

The default printable ticket template is:

- `apps/Store1/Data/ticket-template.txt`
- `apps/Store2/Data/ticket-template.txt`

### Dashboard

The dashboard uses:

- `apps/Dashboard/appsettings.json`

and reads store runtime snapshots from the configured store paths.

## Packaging

Package script:

- `tools/package-commerce-suite.ps1`

Supported modes:

- `ClientClean`
  - for customers
  - no real store runtime data
- `ConfiguredOwner`
  - for the owner
  - includes current configuration and data

Example:

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ClientClean
```

Generated packages are written to:

- `artifacts/`

## GitHub clean export

Export script:

- `tools/export-github-clean.ps1`

This creates a GitHub-safe copy that excludes:

- runtime tokens
- runtime-state files
- cached print jobs
- build output
- packaged artifacts

Example:

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\export-github-clean.ps1"
```

## Do not commit

These should not be pushed to a public or shared repository:

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`
- `apps/Store1/Data/print-jobs/`
- `apps/Store2/Data/print-jobs/`
- packaged zip files
- any real token, printer, or shop credentials

## Documentation

- Architecture: `docs/ARCHITECTURE.md`
- Quick start: `docs/QUICKSTART.md`

## Status

This repository is the maintained source base.

The currently running local business apps can continue to run independently while further refactors happen here.

# Quick Start

## 1. Requirements

- Windows
- .NET 8 SDK
- Local printer driver if you use the store printing apps

## 2. Start the apps

### Start all

Double-click:

- `Start Commerce Suite.cmd`

### Start manually

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

## 3. Open the apps

- Store 1: `http://localhost:5038`
- Store 2: `http://localhost:5039`
- Dashboard: `http://localhost:5048`

## 4. Configure stores

Open the store app in the browser and fill:

- App Key
- App Secret
- Access Token
- Refresh Token
- Shop ID or Shop Cipher
- Printer

## 5. Ticket template

Edit:

- `apps/Store1/Data/ticket-template.txt`
- `apps/Store2/Data/ticket-template.txt`

## 6. Package for customer

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ClientClean
```

## 7. Package for owner

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ConfiguredOwner
```

## 8. Export clean source for GitHub

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\export-github-clean.ps1"
```

## 9. Important

Do not commit:

- `runtime-state.json`
- `print-jobs`
- real tokens
- packaged zip files

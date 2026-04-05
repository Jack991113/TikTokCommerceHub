# Quick Start | 快速上手

## 1. Requirements | 环境要求

- Windows
- .NET 8 SDK
- Local printer driver if you use the store printing apps
- Windows
- .NET 8 SDK
- 如果要用打单程序，需要本地打印机驱动

## 2. Start The Apps | 启动程序

### Start all | 启动全部

Double-click:  
双击：

- `Start Commerce Suite.cmd`

### Start manually | 手动启动

Store 1 / 店铺一:

```powershell
cd apps\Store1
dotnet run
```

Store 2 / 店铺二:

```powershell
cd apps\Store2
dotnet run
```

Dashboard / 数据看板:

```powershell
cd apps\Dashboard
dotnet run
```

## 3. Open The Apps | 打开页面

- Store 1 / 店铺一: `http://localhost:5038`
- Store 2 / 店铺二: `http://localhost:5039`
- Dashboard / 数据看板: `http://localhost:5048`

## 4. Configure Stores | 配置店铺

Open the store app in the browser and fill:  
打开店铺页面后，填写：

- App Key
- App Secret
- Access Token
- Refresh Token
- Shop ID or Shop Cipher
- Printer
- App Key
- App Secret
- Access Token
- Refresh Token
- Shop ID 或 Shop Cipher
- 打印机名称

## 5. Ticket Template | 票面模板

Edit these files if you want to change the printed ticket format:  
如果你要修改票面格式，编辑这些文件：

- `apps/Store1/Data/ticket-template.txt`
- `apps/Store2/Data/ticket-template.txt`

## 6. Package For Customer | 打包给客户

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ClientClean
```

## 7. Package For Owner | 打包给自己

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ConfiguredOwner
```

## 8. Export Clean Source For GitHub | 导出 GitHub 纯净源码

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\export-github-clean.ps1"
```

## 9. Important | 重要说明

Do not commit these files or folders:  
不要把这些文件或目录提交到 GitHub：

- `runtime-state.json`
- `print-jobs`
- real tokens
- packaged zip files
- `runtime-state.json`
- `print-jobs`
- 真实 token
- 打包 zip

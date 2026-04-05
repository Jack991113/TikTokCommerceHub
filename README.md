# TikTokCommerceHub

TikTokCommerceHub is the cleaned-up source tree for the TikTok order printing, bridge capture, and analytics suite.  
TikTokCommerceHub 是 TikTok 打单、桥接抓取、数据看板系统的精简源码仓库。

This repository keeps the business apps usable while making the codebase easier to maintain, package, and publish.  
这个仓库的目标是在不影响现有业务运行的前提下，让代码更容易维护、打包和发布。

## What Is Included | 包含内容

- `apps/Store1`
  - Store 1 auto-print app
  - 店铺一自动打单程序
- `apps/Store2`
  - Store 2 auto-print app
  - 店铺二自动打单程序
- `apps/Dashboard`
  - Sales, reconciliation, product, and streamer analytics dashboard
  - 销售、对账、产品、主播分析看板
- `apps/StoreShared`
  - Shared store logic used by Store 1 and Store 2
  - 店铺一和店铺二共用逻辑
- `src/`
  - New layered architecture foundation (`Domain / Application / Infrastructure / Web`)
  - 新的分层架构基础
- `tools/`
  - Packaging and export scripts
  - 打包和导出脚本
- `docs/`
  - Architecture and usage documents
  - 架构和使用文档

## Main Features | 主要功能

- Two-store local auto-print workflow  
  双店本地自动打单
- TikTok Seller Center bridge capture for buyer handle  
  通过 Seller Center 桥接抓取客户 handle
- Ticket template printing  
  自定义票面模板打印
- Product performance analytics  
  Product ID / 链接商品业绩统计
- Streamer compensation analytics  
  主播薪资与提成分析
- Reconciliation and payout views  
  对账、回款、运费、佣金视图
- Clean packaging for owner deployment and client deployment  
  支持自用包和客户净包

## Default Local Ports | 默认本地端口

- Store 1 / 店铺一: `http://localhost:5038`
- Store 2 / 店铺二: `http://localhost:5039`
- Dashboard / 数据看板: `http://localhost:5048`

## How To Start | 启动方式

### Option 1: Start the full suite | 方式一：启动整套程序

Use the launcher:  
使用启动脚本：

- `Start Commerce Suite.cmd`

### Option 2: Start manually | 方式二：手动启动

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

## How To Configure | 如何配置

### Store apps | 打单程序

The store apps read runtime configuration from:  
打单程序运行配置读取自：

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`

Typical fields include:  
常见配置项包括：

- TikTok app key
- TikTok app secret
- access token
- refresh token
- shop id or shop cipher
- printer name
- paper size
- print options

### Ticket template | 票面模板

Default printable template files:  
默认票面模板文件：

- `apps/Store1/Data/ticket-template.txt`
- `apps/Store2/Data/ticket-template.txt`

### Dashboard | 数据看板

The dashboard uses:  
数据看板主要使用：

- `apps/Dashboard/appsettings.json`

and reads runtime snapshots from the configured store paths.  
并从配置好的店铺运行状态文件中读取数据。

## Packaging | 打包

Package script / 打包脚本:

- `tools/package-commerce-suite.ps1`

Supported modes / 支持模式:

- `ClientClean`
  - for customers
  - 发给客户的净包
- `ConfiguredOwner`
  - for the owner
  - 带当前配置的自用包

Example / 示例:

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\package-commerce-suite.ps1" -Mode ClientClean
```

Generated packages are written to:  
生成的打包目录位置：

- `artifacts/`

## GitHub Clean Export | GitHub 纯净导出

Export script / 导出脚本:

- `tools/export-github-clean.ps1`

This creates a GitHub-safe copy that excludes:  
这个脚本会生成适合上传 GitHub 的纯净版，并排除：

- runtime tokens
- runtime-state files
- cached print jobs
- build output
- packaged artifacts
- 真实 token
- runtime-state.json
- 订单缓存
- 编译输出
- 打包产物

Example / 示例:

```powershell
powershell -ExecutionPolicy Bypass -File ".\tools\export-github-clean.ps1"
```

## Do Not Commit | 不要提交到 GitHub 的内容

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`
- `apps/Store1/Data/print-jobs/`
- `apps/Store2/Data/print-jobs/`
- packaged zip files
- any real token, printer, or shop credentials
- 打包 zip
- 真实 token、打印机、店铺配置

## Documentation | 文档

- Architecture / 架构说明: `docs/ARCHITECTURE.md`
- Quick Start / 快速上手: `docs/QUICKSTART.md`

## Status | 当前状态

This repository is the maintained source base.  
这个仓库是当前维护中的源码主仓库。

The currently running local business apps can continue to run independently while further refactors happen here.  
当前正在运行的本地业务程序可以继续独立运行，后续重构会在这里持续推进。

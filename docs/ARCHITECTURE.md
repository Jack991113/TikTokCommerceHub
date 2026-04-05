# Architecture | 架构说明

## Goal | 目标

The old suite grew because store apps, packaging outputs, build outputs, and runtime data were mixed together.  
旧系统体积变大的主要原因，是店铺程序、打包产物、编译输出和运行数据混在一起。

This repository separates:  
这个仓库现在把这些层次拆开：

- maintainable source code  
  可维护源码
- shared business logic  
  共用业务逻辑
- runtime data  
  运行时数据
- packaging scripts  
  打包脚本

without breaking the currently running local apps.  
同时不影响当前正在运行的本地程序。

## Top-Level Structure | 顶层结构

```text
TikTokCommerceHub/
  apps/
    Store1/
    Store2/
    StoreShared/
    Dashboard/
  src/
    TikTokCommerceHub.Domain/
    TikTokCommerceHub.Application/
    TikTokCommerceHub.Infrastructure/
    TikTokCommerceHub.Web/
  docs/
  tools/
```

## App Layout | 应用结构

### Store1 and Store2 | 店铺一和店铺二

These are the local printing apps.  
这两套是本地打单程序。

Responsibilities | 负责内容:

- poll TikTok order APIs  
  轮询 TikTok 订单接口
- capture buyer handle via Seller Center bridge  
  通过 Seller Center 桥接抓取买家 handle
- render and print tickets  
  渲染并打印票面
- expose local management UI  
  提供本地管理页面

Most of the code is shared through `apps/StoreShared`.  
大部分共用逻辑已经抽到 `apps/StoreShared`。

### Dashboard | 数据看板

This is the analytics app.  
这是独立的数据分析看板。

Responsibilities | 负责内容:

- sales overview  
  销售总览
- reconciliation  
  对账与回款
- product performance  
  Product ID / 链接商品业绩
- streamer compensation  
  主播薪资与提成
- export and workbook generation  
  导出与 Excel 工作簿生成

### New Layered Foundation | 新分层基础

The `src/` tree is the forward-looking architecture:  
`src/` 是后续重构的目标分层：

- `Domain`
  - business entities and shared rules  
    领域对象和核心规则
- `Application`
  - use cases and DTOs  
    用例和数据传输对象
- `Infrastructure`
  - file/runtime readers and external integration support  
    文件读取、运行时状态读取、外部集成支持
- `Web`
  - lightweight web endpoints  
    轻量 Web 接口

This allows gradual migration from the older app-heavy structure into a cleaner shared backend.  
这样可以逐步把旧的重型应用结构迁移到更干净的共享后端。

## Runtime Data | 运行数据

Runtime data is intentionally kept outside the Git-safe source flow:  
运行数据刻意保持在 Git 安全边界之外：

- `apps/Store1/Data/runtime-state.json`
- `apps/Store2/Data/runtime-state.json`
- `apps/Store1/Data/print-jobs/`
- `apps/Store2/Data/print-jobs/`

These files may contain:  
这些文件可能包含：

- tokens
- printer settings
- cached order data
- print history
- token
- 打印机设置
- 订单缓存
- 打印历史

They must not be pushed to GitHub.  
这些内容绝对不能提交到 GitHub。

## Packaging Strategy | 打包策略

Packaging is script-driven through:  
打包通过脚本完成：

- `tools/package-commerce-suite.ps1`

Modes | 模式:

- `ClientClean`
  - safe to send to customers  
    适合发给客户
- `ConfiguredOwner`
  - includes current configuration for owner use  
    带当前配置，适合自用

## GitHub Export Strategy | GitHub 导出策略

GitHub-safe export is created by:  
GitHub 纯净导出由这个脚本生成：

- `tools/export-github-clean.ps1`

The export rewrites paths where needed and excludes runtime-sensitive files.  
导出时会重写必要路径，并排除运行敏感文件。

## Why This Is Smaller Than The Old Tree | 为什么比旧项目更瘦

The old project looked huge mainly because of:  
旧项目看起来很大，主要因为：

- duplicated Store1 and Store2 source  
  店铺一和店铺二大量重复
- `bin/obj/publish`  
  编译输出混在目录里
- packaged artifacts  
  打包产物很多
- validation and staging directories  
  验证目录和阶段目录很多
- runtime data mixed into source  
  运行数据混进源码树

After extracting shared code, the maintainable source is much smaller and easier to reason about.  
在抽出共享代码后，真正需要维护的源码已经明显变小，也更容易继续重构。

## Next Refactor Direction | 下一步重构方向

Recommended next steps:  
建议继续做的方向：

1. Keep Store1 and Store2 running as-is.  
   保持当前运行中的店铺程序稳定。
2. Continue moving duplicated frontend pieces into shared assets.  
   继续抽取重复前端代码。
3. Move more dashboard query logic into `src/Application`.  
   把更多看板查询逻辑迁到 `src/Application`。
4. Reduce app-specific startup code even further.  
   继续减少每个应用自己的启动样板代码。
5. Eventually make a cleaner deployable split:
   - server-side business backend
   - local print agent  
   最终形成更清晰的部署结构：
   - 服务器业务后端
   - 本地打印代理

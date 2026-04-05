# TikTokCommerceHub

这是给现有 TikTok 打单/看板系统准备的下一代骨架。

目标：

- 不碰当前正在运行的程序
- 先把高并发读查询从旧系统拆出来
- 后续逐步迁移统计、对账、产品、主播薪资、打印后台

当前入口：

- 解决方案：`TikTokCommerceHub.sln`
- Web 项目：`src/TikTokCommerceHub.Web`
- 架构说明：`docs/ARCHITECTURE.md`

当前能力：

- 轻量快照接口：`/api/dashboard/snapshot`
- 输出缓存
- 内存缓存
- 响应压缩
- 速率限制
- 健康检查：`/health`

后续迁移建议：

1. 先迁移总览读模型
2. 再迁移对账
3. 再迁移产品分析
4. 再迁移主播薪资
5. 最后才动打印后台

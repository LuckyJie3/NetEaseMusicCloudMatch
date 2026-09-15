# Changelog

本项目的重要变更记录在此文件中。

## [1.0.0] - 2026-09-15

首个公开 Windows 重写版本。

### 功能

- 基于 .NET 10、WPF 和 MVVM 的全新桌面应用；
- 通过用户手工粘贴的完整 Cookie 验证登录；
- 显示账号信息、云盘容量和分页歌曲列表；
- 支持通过歌曲 ID 或网易云单曲 URL 预览目标歌曲；
- 支持确认匹配以及二次确认取消匹配；
- 支持浅色、深色和跟随系统主题；
- 提供可展开的本地操作日志、脱敏响应 JSON 和日志清除功能。

### 安全

- 可选登录状态使用 Windows DPAPI `CurrentUser` 加密保存；
- Cookie 只发送到配置允许的网易云官方 HTTPS 域名；
- 禁止自动重定向携带 Cookie，避免凭证跨 Host 转发；
- 日志过滤 Cookie、Authorization、`MUSIC_U`、`__csrf` 等敏感字段；
- 不读取浏览器 Cookie 数据库，不使用随机伪造 IP Header；
- 匹配写请求不会在结果不确定时自动重试。

### 工程

- Endpoint、Host 和 HTTP Method 集中管理；
- 使用 `HttpClientFactory`、异步请求和 CancellationToken；
- 提供安全 API Probe、Windows x64 发布脚本以及 46 个单元测试；
- GitHub Actions 自动执行 Release 编译和测试。

[1.0.0]: https://github.com/LuckyJie3/NetEaseMusicCloudMatch/releases/tag/v1.0.0

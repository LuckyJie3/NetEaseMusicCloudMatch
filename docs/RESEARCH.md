# NeteaseMusicCloudMatch 重写研究记录

> 研究日期：2026-09-12（Asia/Shanghai）
> 本文只记录从源码和安全只读探针中实际确认的行为，不把网易云私有 API 当作稳定公开协议。

## 1. 研究范围与版本

### 旧 Windows 项目

原始项目：[wuhenge/NeteaseMusicCloudMatch](https://github.com/wuhenge/NeteaseMusicCloudMatch)。工作区实际扫描的源码位于 `NeteaseMusicCloudMatch/`，是用户预先下载的 EVAyo fork 快照；下列源码行为以该本地快照为准。macOS 参考项目的 README 也将其 Windows 上游明确归因到 wuhenge。

- `NeteaseMusicCloudMatch.csproj`：旧式非 SDK 项目，`OutputType=WinExe`，目标为 .NET Framework 4.0，依赖 WinForms、Newtonsoft.Json 13.0.1、Fody/Costura 以及二维码程序集。
- `Program.cs`：`[STAThread]` 入口，在 Windows Vista 及以上调用 `SetProcessDPIAware()`，最终执行 `Application.Run(new Form1())`。
- `Form1.cs`：登录、HTTP 调用、JSON 解析、分页、搜索、验证、匹配和 UI 提示都集中在一个窗体中。
- `Form1.Designer.cs`：三个 GroupBox 组成的 WinForms 界面；二维码/用户信息、云盘 DataGridView、匹配输入区域；1 秒 Timer 默认启用。
- `CommonHelper.cs`：包装同步 GET、随机 IP 头、二维码、同步头像下载、JSON 检测、格式化和 Win32 INI 读写。
- `HttpHelper.cs`：2011 年风格的通用 `HttpWebRequest` 封装（标注版本 2.2.97），同步发送、手工 Cookie/Header/Post/代理/证书处理。
- `packages.config`：Costura.Fody 5.6.0、Fody 6.5.3、Newtonsoft.Json 13.0.1，均面向 net40。
- `app.config`：仅声明 `.NETFramework,Version=v4.0`。
- `Properties/Settings.settings`：没有应用设置；真正的登录配置不在这里，而在程序目录下的 `Config.ini`。
- 根 `README.md`：文档明确把“歌曲 ID 输入 0”作为取消匹配方式。
- 本地快照根 `LICENSE`：MIT，Copyright (c) 2023 Healer；这是所检查 fork 快照中的法律声明，不用于替代上游项目 attribution。

### macOS 参考项目

仓库：[joeyee233/NetEaseMusicCloudMatch](https://github.com/joeyee233/NetEaseMusicCloudMatch)，研究时克隆的 `main` HEAD：

- commit：`05a36e695d3a996bbbf6ddc13272d51a4b3fe8f6`
- commit 时间：2025-09-09 16:51:38 +08:00
- 本地只读参考副本：`.reference/NetEaseMusicCloudMatch-mac/`

重点完整阅读了 `LoginManager.swift`、`UserManager.swift`、`NetworkManager.swift`、`SongManager.swift`、`LoginView.swift`、`ContentView.swift`、`Song.swift` 和 `SongListView.swift`。该项目 README 说明通过浏览器 DevTools 复制完整 Cookie 登录。

## 2. 旧 Windows 项目的真实行为

### 2.1 登录与配置

入口和启动流程：

1. `Program.Main()` 创建 `Form1`。
2. `Form1_Load()` 从 `Config.ini` 的 `[NeteaseMusic]` 节读取 `LoginCheck`。
3. 若自动登录已勾选，再从同一节读取 `Cookie`；Cookie 非空时直接请求用户信息和云盘，不先验证凭证格式。
4. 否则进入二维码登录。

对应源码：

- `NeteaseMusicCloudMatch/NeteaseMusicCloudMatch/Program.cs`：`Main()`。
- `NeteaseMusicCloudMatch/NeteaseMusicCloudMatch/Form1.cs`：`Form1_Load()`、`checkBox1_CheckedChanged()`。
- `NeteaseMusicCloudMatch/NeteaseMusicCloudMatch/CommonHelper.cs`：`ConfigFilePath`、`Read()`、`Write()`。

旧二维码流程：

- `LoadQrCodeImage()` 以 GET 请求 `/api/login/qrcode/unikey?type=1`，读取 `unikey`，生成指向 `https://music.163.com/login?codekey={unikey}` 的本地二维码。
- `timer1_Tick()` 每秒以 GET 请求 `/api/login/qrcode/client/login?type=1&key={unikey}`。
- API code `800` 时清空内存 Cookie 并重新生成二维码；code `803` 时直接读取响应 `Set-Cookie` 字符串，执行 `Replace(",", ";")`，然后作为 `wyCookie` 使用并写入 INI。

对应源码：`Form1.LoadQrCodeImage()`、`Form1.timer1_Tick()`；`HttpHelper.GetData()` 从 `response.Headers["set-cookie"]` 填充 `HttpResult.Cookie`。

结论：旧版不是“完整 Cookie 手工粘贴登录”，而是二维码接口 + 响应 Set-Cookie；自动登录时 Cookie 以明文保存在程序目录 `Config.ini`。`Properties/Settings.settings` 为空。

### 2.2 Cookie 如何进入请求

`Form1` 把全局静态字符串 `wyCookie` 传给 `CommonHelper.GetHtml(url, wyCookie)`。后者创建 `HttpItem` 并将其 `Cookie` 属性交给 `HttpHelper.SetCookie()`；最终代码为：

```csharp
request.Headers[HttpRequestHeader.Cookie] = item.Cookie;
```

旧版没有按 Cookie 名称选择性提取，也没有 CookieContainer 域校验；字符串原样进入 Cookie Header。二维码响应的 Set-Cookie 仅以简单的逗号到分号替换进行拼接。

对应源码：`CommonHelper.GetHtml()`、`HttpHelper.SetCookie()`。

### 2.3 用户信息

`Form1.LoadUIDName()`：

- GET `https://music.163.com/api/nuser/account/get`
- 携带 `wyCookie`
- 当 JSON `code == 200` 时读取 `profile.userId`、`profile.nickname`、`profile.avatarUrl`
- 头像另由 `CommonHelper.GetImage()` 用同步 `HttpWebRequest` 下载（该图片请求不带账号 Cookie）

旧代码只判断 `code`，但 `profile` 缺失时不会明确判定 Cookie 无效，也不会返回登录页。

### 2.4 云盘容量与列表

容量：`Form1.LoadCloudInfo()` 使用 GET：

```text
/api/v1/cloud/get?limit=0
```

从响应读取 `size` 和 `maxSize`。

列表：`Form1.GetCloudData()` 使用 GET：

```text
/api/v1/cloud/get?limit=200&offset=(pageIndex-1)*200
```

当 `code == 200` 且 `count > 0` 时遍历 `data`，读取：

- `songId`
- `fileName`
- `fileSize`
- `addTime`

滚动到表尾时 `ScrollReader()` 直接增加页码并启动新的 `Thread(GetCloudData)`。源码没有总页数/末页停止条件，也没有并发门控，重复滚动可能产生多次请求。搜索则把当前 DataGridView 转成 DataTable 后在内存筛选文件名；搜索会清空现有表格，且循环中重复 `Rows.Clear()`，多条命中时最终只会留下最后一条，这是明确缺陷。

### 2.5 云盘歌曲验证

`Form1.CheckCloudFileStatus(songId)` 使用 GET：

```text
/api/cloud/get/byids?songIds=[cloudSongId]
```

携带 Cookie。`code == 200` 且 `data` 数组非空即认为云盘歌曲存在；其他 API code、空数组、非 JSON 和异常都折叠为失败。

### 2.6 目标歌曲验证

`Form1.CheckSongStatus(songId)`：

- 若输入是字符串 `"0"`，不请求歌曲详情，而是弹出“确定要取消匹配吗？”；确认后返回可继续状态。
- 其他值使用 GET `/api/song/detail/?ids=[targetSongId]`。
- `code == 200` 且 `songs` 数组非空即认为目标歌曲存在。

旧版不展示目标歌曲的名称、歌手、专辑或封面预览。

### 2.7 歌曲 URL 解析

`Form1.GetUrlMatchId()` 使用多段正则，试图从普通网页、移动链接和 `com/{type}/{id}` 形式提取类型与 ID，再用字符串包含关系拒绝 `toplist`、`playlist`、`album`，只接受包含 `song` 的类型。

这一实现存在以下问题：

- 不是基于 `Uri`/host/path/query 的结构化验证；
- `\w+` 和覆盖写入匹配结果使行为脆弱；
- 没有可靠校验 host 为网易云官方域名；
- 纯数字输入在按钮流程中没有严格的数字/正数验证；
- URL 解码、参数顺序、额外 query、fragment 的处理依赖偶然匹配。

新版只保留“纯数字或明确的网易云单曲 URL → song ID”的概念，解析器需要重写。

### 2.8 执行匹配与取消匹配

`Form1.button3_Click()` 先取：

```text
uid  = 当前登录用户 userId
sid  = DataGridView 选中行的云盘 songId
asid = 用户输入或 URL 解析出的目标歌曲 ID
```

验证云盘项和目标歌曲后，使用 GET：

```text
/api/cloud/user/song/match?userId={uid}&songId={sid}&adjustSongId={asid}
```

这里从变量来源可以确定：

- `songId` = 云盘歌曲本身的 ID；
- `adjustSongId` = 目标网易云曲库歌曲 ID；
- `adjustSongId=0` = 取消关联。

`code == 200` 时显示成功并重新加载云盘列表。旧版会把完整匹配响应 `Console.WriteLine(html)`，API 错误时也可能把原始响应直接弹给用户。

## 3. macOS 参考实现的真实行为

### 3.1 Cookie 登录

`Views/LoginView.swift` 提供多行 `TextEditor`，用户粘贴完整 Cookie 后调用 `LoginManager.loginWithCookie(cookie)`。它不会自动读取浏览器 Cookie。

`LoginManager.loginWithCookie()`：

1. 把完整 Cookie 同时放入 `LoginManager.userToken` 和 `UserManager.setTemporaryToken()`。
2. `getUserInfo()` 以 POST 请求 `https://music.163.com/api/nuser/account/get`，无 body 参数。
3. 响应存在 `profile` 字典即登录成功，调用 `UserManager.updateUserInfo(from:token:)`。
4. 成功后加载第一页云盘；失败时清除用户信息。

### 3.2 Cookie 传输和持久化

`NetworkManager.request()` 每次请求都从 `UserManager.getToken()` 取完整 token，并设置：

```swift
request.setValue(userToken, forHTTPHeaderField: "Cookie")
```

所有请求默认 `Content-Type: application/x-www-form-urlencoded`。GET 参数写入 query，POST/PUT 参数组成 form body。

但该参考实现有两项不能复制的安全问题：

- `UserInfo` 的 Codable 字段包含 `token`；`UserManager.updateUserInfo()` 把整个对象编码后写入 `UserDefaults`，等同于持久化完整 Cookie，未见 Keychain 或加密保护。
- 开启 `NetworkManager.isDebugMode` 后，会打印完整 Cookie、全部请求头和响应头，可能泄漏 Cookie/Set-Cookie。

### 3.3 用户与云盘

- 用户信息：`LoginManager.getUserInfo()` 使用 POST `/api/nuser/account/get`。
- 云盘：`SongManager.fetchPage(page:limit:)` 使用 POST `/api/v1/cloud/get`，form body 为 `limit`、`offset`，默认页大小 200。
- 响应 `code == 200` 时读取 `count`、`size`、`maxSize` 和 `data`。
- 响应 `code == 301` 时调用 `LoginManager.logout()`，清空用户与云盘状态。

`Song.init(json:)` 从每个云盘元素的 `simpleSong` 读取 ID、名称、歌手、专辑、封面、时长，再读取顶层 `fileName`、`fileSize`、`bitrate`、`addTime`。它要求 `simpleSong.id` 和 `simpleSong.name` 必须存在，否则整行被 `compactMap` 丢弃；这不是新版所需的容错行为。

### 3.4 匹配

`SongManager.matchCloudSong(cloudSongId:matchSongId:)` 使用 GET：

```text
/api/cloud/user/song/match?userId={UserManager.userId}&songId={cloudSongId}&adjustSongId={matchSongId}
```

`code == 200` 即成功；若有 `matchData`，尝试转换成 `Song` 并更新当前数组项，否则只返回成功。参数含义与旧 Windows 源码一致。

macOS 当前实现没有调用 `/api/cloud/get/byids` 或 `/api/song/detail/`，也没有目标歌曲预览。它只用当前页数组确认云盘项存在，然后允许直接编辑列表中的歌曲 ID 并按回车发起匹配；成功后还把行的 `id` 改成输入值。它也没有明确的“取消匹配”按钮和二次确认。

### 3.5 QR 与加密代码现状

`LoginManager` 仍保留 QR 状态、`getQRCodeKey()`、固定 `secretKey`/`encSecKey` 字段，但 `startLoginProcess()` 中真正获取二维码的 Task 已被注释。固定密钥也没有进入当前账号、云盘或匹配请求。不能据此得出 API 需要 weapi/eapi 加密。

## 4. Windows 与 macOS 请求差异

| 功能 | 旧 Windows | macOS 参考实现 | 新版首选 |
|---|---|---|---|
| 登录入口 | QR unikey + 每秒轮询 | 用户粘贴完整 Cookie | 完整 Cookie；第一版不做 QR |
| Account | GET `/api/nuser/account/get` | POST 同一路径，无参数 | POST；通过 Probe 验证 |
| Cloud | GET，`limit/offset` 在 query | POST，form body `limit/offset` | POST form，默认 200，显式分页 |
| Cloud by IDs | GET，query `songIds=[id]` | 未使用 | 保留为显式云盘项验证能力 |
| Song detail | GET，query `ids=[id]` | 未使用 | 用于目标歌曲预览 |
| Match | GET，三个 query 参数 | GET，三个 query 参数；支持 `matchData` | GET；写操作不自动重试 |
| Cookie 传递 | 原样 Cookie Header | 原样 Cookie Header | 完整 Cookie 仅发往允许的官方 host |
| Cookie 保存 | 程序目录 INI 明文 | UserDefaults 中 Codable 明文数据 | 未勾选仅内存；勾选后 Windows DPAPI CurrentUser |
| 登录失效 | 没有统一处理 | Cloud code 301 时退出 | API 错误分类 + AuthService 通知 + 返回登录页 |

两个被检查的源码版本都只把业务 API 发往 `https://music.163.com`；没有发现 `interface.music.163.com`。初版因此按参考源码使用 main host。2026-09-13 用户侧真实日志显示所有读取接口成功，但 Match 在 main host 连续返回业务 `code=400 / 当前歌曲不支持匹配`；而该功能最初公开的抓包资料明确记录写接口原始 host 为 `interface.music.163.com`。新版据此只把 Match/Cancel 切换到 interface host，读取接口不变，写请求也不会自动跨 host 重试。

## 5. 2026-09-12 无凭证可达性结果

当前环境没有设置 `NETEASE_MUSIC_COOKIE`。研究阶段仅向 `music.163.com` 发出不带 Cookie 的读请求，没有调用 Match：

| 请求 | HTTP | API/必要字段 | 结论 |
|---|---:|---|---|
| GET Account | 200 | `code=200`, `account=null`, `profile=null` | 路由可达；不能只凭 code=200 判定登录 |
| POST Account | 200 | `code=200`, `account=null`, `profile=null` | POST 路由可达；必须要求非空有效 profile |
| GET Cloud，limit=1/offset=0 | 200 | `code=301` | GET 路由可达并要求登录 |
| POST Cloud，form limit=1/offset=0 | 200 | `code=301` | POST 路由可达并要求登录 |
| GET Cloud by IDs | 200 | `code=301` | 路由可达并要求登录 |
| GET Song detail，ID 186137 | 200 | 返回 ID 186137（双截棍）的歌曲数据 | 公开详情路由可达 |

以上结果不证明携带 Cookie 后仍能读取云盘，也不证明 Match 写操作可用。认证与写操作必须由开发者提供 Cookie 后分别实测；Match 只能显式手动触发。

## 6. 明确废弃的旧代码和行为

新版不迁移以下实现：

- `Form1` 单体业务架构和 UI 事件中的网络逻辑；
- `HttpHelper.cs`、`HttpWebRequest`、`WebRequest`、同步网络 I/O；
- `CommonHelper.GetHtml()`；
- `x-from-src`、`X-Real-IP`、`X-Forwarded-For` 三个随机伪造 IP Header；
- QR 登录、1 秒 WinForms Timer 轮询及相关二维码依赖；
- Win32 INI 读写和明文 Cookie；
- Newtonsoft `JObject`/`JArray` 动态解析；
- 裸 `Thread`、跨线程 `Invoke` 式数据加载；
- 把服务器原始 JSON、异常消息直接 MessageBox 给普通用户；
- 把匹配响应、Cookie 或完整请求/响应 Header 写入控制台/日志；
- macOS 版把 token 编码进 UserDefaults 的做法；
- macOS 版 Debug 模式打印 Cookie/headers 的做法；
- 无证据的固定加密密钥或 weapi/eapi 实现；
- 客户端任意信任 TLS 证书的通用回调。旧 `HttpHelper.CheckValidationResult()` 恒返回 true，虽当前业务调用未设置证书路径而没有启用该回调，仍不应保留。

注：指定检查的旧源码中没有 `Thread.Sleep`，但存在同步请求、裸 Thread 和 Timer；新版统一使用 async/await、CancellationToken 和 `IHttpClientFactory`。

## 7. 可保留的协议与业务不变量

在后续带 Cookie 实测推翻之前，以下内容作为“源码交叉验证过的兼容基线”：

- 当前登录用户 UID 来自 account 响应的 `profile.userId`；
- 云盘列表使用 `limit`/`offset` 分页；
- 云盘项目的核心标识为顶层 `songId`（但 DTO 要容忍字段缺失/类型异常）；
- 目标曲库歌曲在匹配前通过 song detail 查询并让用户确认；
- Match 的 `songId` 永远是云盘歌曲 ID；
- Match 的 `adjustSongId` 永远是目标曲库歌曲 ID；
- 取消匹配等价于 `adjustSongId=0`，但 UI 必须提供明确按钮和二次确认；
- Match `code==200` 视为成功；有 `matchData` 时优先局部更新，否则重新拉取当前页；
- Match 属于写请求，响应不确定时不能盲目自动重试。

## 8. 研究阶段结论

较新的 macOS 版本为 Account 和 Cloud 采用 POST，其中 Cloud 明确使用 form body；Match 仍是 GET query，且参数含义和旧 Windows 版完全一致。旧版的 QR、随机 IP、同步请求、明文 INI、动态 JSON 与单窗体架构均应废弃。新版 API 层需要把 host、path、method 集中起来，同时把“源码确认”“未认证可达”“带账号验证”“写操作验证”四种证据状态严格区分。

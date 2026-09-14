# 网易云私有 API 兼容性与 Probe 记录

> 状态：已实现，并根据 2026-09-13 真实账号日志修订
>
> 更新日期：2026-09-13
> 注意：这些是网易云音乐网页使用过的私有接口，不是稳定公开 API。实现必须允许集中更换 host、path 和 method。

## 1. 默认 Host 与约束

读取接口默认 Base Host：

```text
https://music.163.com
```

匹配写接口 Host：

```text
https://interface.music.163.com
```

读取接口继续采用旧 Windows 与当前 macOS 参考实现共同使用的 `music.163.com`。2026-09-13 的真实账号日志证明 Account、Cloud、Cloud by IDs 和 Song detail 均可用。Match 在 `music.163.com` 连续返回 HTTP 200、API `code=400`、`当前歌曲不支持匹配`；同时该功能最初的抓包资料明确给出的原始写入地址是 `interface.music.163.com/api/cloud/user/song/match`。因此只把 Match/Cancel 集中切换到 interface host，不对写请求做失败后自动换 host 重试。

实现约束：

- Host 仅在 `NeteaseEndpoints`/options 中配置；View 与 ViewModel 不持有 URL。
- Cookie 只能发送到明确允许的网易云官方 host，禁止跟随跨 host 重定向时泄漏 Cookie。
- 不添加随机 IP Header。
- 不添加 weapi/eapi 加密，除非真实请求给出必要证据。
- 所有响应同时检查 HTTP status 与 JSON `code`、`message`、`msg`。
- 诊断记录 Endpoint、状态码、耗时及经过结构化脱敏和限长的响应 JSON；禁止记录 Cookie、Set-Cookie、Authorization 或未经处理的原始响应。

## 2. Endpoint 矩阵

证据等级：

- **W**：旧 Windows 源码确认。
- **M**：macOS 参考源码确认（commit `05a36e6`）。
- **U**：2026-09-12 无 Cookie 实际可达性确认。
- **A**：需要有效账号 Cookie 的真实读取验证。
- **X**：需要开发者明确授权的真实写入验证。

| 操作 | Method | Path | 参数位置 | 关键参数 | 证据 | 当前结论 |
|---|---|---|---|---|---|---|
| GetAccount | POST（首选） | `/api/nuser/account/get` | 无 | 无 | M, U；W 使用 GET | 路由可达；有效 profile 待 A |
| GetCloudSongs | POST（首选） | `/api/v1/cloud/get` | form body | `limit`, `offset` | M, U；W 使用 GET query | 未登录 code 301；待 A |
| GetCloudSongById | GET | `/api/cloud/get/byids` | query | `songIds=[cloudSongId]` | W, U | 未登录 code 301；待 A |
| GetSongDetail | GET | `/api/song/detail/` | query | `ids=[targetSongId]` | W, U | 公开歌曲实测返回；认证非必要但仍携带会话时须脱敏 |
| MatchCloudSong | GET | `/api/cloud/user/song/match` | query | `userId`, `songId`, `adjustSongId` | W, M；2026-09-13 在 main host 得到业务 400 | 改用 interface host；仍待用户复测成功 |
| CancelCloudMatch | GET（同 Match） | `/api/cloud/user/song/match` | query | `userId`, `songId`, `adjustSongId=0` | W；M 的通用方法可表达但无显式 UI | 使用 interface host；不自动调用 |

历史 QR 接口仅用于记录，不进入第一版：

| 操作 | Method | Path | 参数 |
|---|---|---|---|
| GetQrKey | GET | `/api/login/qrcode/unikey` | `type=1` |
| PollQrLogin | GET | `/api/login/qrcode/client/login` | `type=1`, `key={unikey}` |

## 3. 请求与响应契约

### 3.1 GetAccount

首选请求：

```http
POST /api/nuser/account/get
Content-Type: application/x-www-form-urlencoded
Cookie: <完整 Cookie，仅运行时注入>
```

没有业务 body 参数。旧 Windows 版曾使用 GET；macOS `LoginManager.getUserInfo()` 使用 POST。

成功条件不能只写 `code == 200`。无 Cookie 实测也得到：

```json
{"code":200,"account":null,"profile":null}
```

因此登录成功至少要求：

- HTTP 成功；
- API `code == 200`；
- `profile` 非 null；
- `profile.userId` 能解析为正整数；
- 昵称、头像 URL 均按 nullable 处理。

无有效 profile 时，面向用户统一提示：“Cookie 无效或已过期，请重新登录网易云网页版后复制新的 Cookie。”

### 3.2 GetCloudSongs

首选请求：

```http
POST /api/v1/cloud/get
Content-Type: application/x-www-form-urlencoded
Cookie: <完整 Cookie>

limit=200&offset=0
```

分页：`offset = (page - 1) * limit`。默认 `limit=200`，必须支持多页，不在列表滚动事件里无条件并发追加。

预期顶层字段均需可空/容错：

```text
code, message, msg, count, size, maxSize, data
```

单个 `data` 项可能包含：

```text
songId, fileName, fileSize, bitrate, addTime, simpleSong
```

`simpleSong` 中可能包含：

```text
id, name, ar[], al, dt
```

解析原则：

- 数字可能以 JSON number 或 string 出现；
- 字段缺失时 UI 显示“—”；
- 单条异常不得让整页反序列化失败；
- `code=301` 归类为 CookieExpired，通知 AuthService 并回到登录页；
- `count` 是总数量，不代表本页 `data` 长度。

### 3.3 GetCloudSongById

请求：

```http
GET /api/cloud/get/byids?songIds=%5B{cloudSongId}%5D
Cookie: <完整 Cookie>
```

源码中的未编码形式为 `songIds=[id]`。客户端应通过正规 query 构造器编码，不拼接未验证文本。

成功语义基线：HTTP 成功、`code == 200`、`data` 数组至少一项；空数组归类为 CloudSongNotFound。`code=301` 归类为 CookieExpired。

### 3.4 GetSongDetail

请求：

```http
GET /api/song/detail/?ids=%5B{targetSongId}%5D
```

源码中的未编码形式为 `ids=[id]`。目标 ID 先由 `SongIdParser` 验证为正整数。

成功语义基线：HTTP 成功、`code == 200`（若字段存在）、`songs` 数组至少一项，且返回 song ID 与目标一致。空数组归类为 SongNotFound。

预览需要宽容读取：

- `songs[0].id`
- `songs[0].name`
- `songs[0].artists[].name`（旧响应形态）
- `songs[0].album.name`
- `songs[0].album.picUrl`

如当前响应采用 `ar`/`al` 形态，DTO/归一化层应兼容，但必须用夹具和实测证明后再加入。

### 3.5 MatchCloudSong

请求基线（GET）：

```http
GET https://interface.music.163.com/api/cloud/user/song/match?userId={userId}&songId={cloudSongId}&adjustSongId={targetSongId}
Cookie: <完整 Cookie>
```

不可改变的参数语义：

| 参数 | 含义 |
|---|---|
| `userId` | 当前 Cookie 对应的账号 UID |
| `songId` | 当前选中的云盘歌曲 ID |
| `adjustSongId` | 目标网易云曲库歌曲 ID |

取消匹配：

```text
adjustSongId=0
```

客户端 API 应提供明确的 `CancelMatchAsync(userId, cloudSongId)` 或等价服务方法，UI 不要求用户输入 0。

成功基线：HTTP 成功且 `code == 200`。若响应含 `matchData`，优先用其刷新当前行；否则重新读取当前页或当前云盘项。

这是写操作：禁止对超时、连接中断或 5xx 做无条件自动重试。响应不确定时应提示用户刷新核对当前状态。

若返回 `code=400` 且消息为“当前歌曲不支持匹配”，归类为 `CloudSongNotMatchable`。这表示请求已进入网易云业务层，但服务端拒绝了当前云盘条目的纠正；常见可能性包括条目类型、当前关联状态或曲库可用性限制。它不是 HTTP、Cookie 或目标歌曲不存在错误。由于接口私有，客户端不能仅凭这句消息进一步断言一定是版权原因。

## 4. Cookie 行为与安全边界

输入：仅接受用户从浏览器 DevTools 手工复制的完整 Cookie 字符串，或开发 Probe 的环境变量 `NETEASE_MUSIC_COOKIE`。禁止自动读取 Chrome/Edge Cookie 数据库。

内存使用：

- 登录验证前临时保存在内存；
- API 请求用完整字符串设置 `Cookie` Header；
- 不在异常、ToString、DebuggerDisplay、日志 scope 或 telemetry 中携带值。

持久化：

- 未勾选“记住登录状态”：不落盘；
- 勾选：使用 `ProtectedData.Protect(..., DataProtectionScope.CurrentUser)`；
- 建议路径 `%LOCALAPPDATA%\NeteaseMusicCloudMatch\session.dat`；
- 退出登录删除该文件；
- UID、昵称、头像 URL 可以作为非敏感资料保存，但不能与明文 Cookie 一起序列化。

日志脱敏至少覆盖（大小写不敏感）：

```text
Cookie
Set-Cookie
Authorization
MUSIC_U
__csrf
MUSIC_A
MUSIC_R_T
```

还应过滤常见 `name=value`、Header JSON/字典形式以及异常/响应摘要中出现的同名字段。日志不得记录完整请求/响应 Header。

桌面应用日志中的响应 JSON 使用以下额外约束：

- 写入前解析 JSON，并递归替换凭证字段为 `[REDACTED]`；
- 单个数组最多保留 12 项，对象深度、字段数量和字符串长度设上限；
- 日志页只展示该脱敏副本，不保留另一份原始响应；
- 无法解析为 JSON 时只保存经过凭证过滤和长度限制的诊断文本；
- 用户可在应用内二次确认后清除本机滚动日志，清除动作不影响 DPAPI 会话文件。

## 5. 错误映射初稿

| 条件 | 分类 | 用户消息方向 |
|---|---|---|
| HTTP 401/403 | Unauthorized | 登录状态无效或无权限 |
| API code 301；Account 无有效 profile | CookieExpired | Cookie 无效或已过期 |
| Song detail 为空/明确不存在 code | SongNotFound | 找不到目标歌曲 |
| Cloud by IDs 为空/明确不存在 code | CloudSongNotFound | 云盘歌曲不存在或已变化 |
| Match code 400 且消息为“当前歌曲不支持匹配” | CloudSongNotMatchable | 网易云拒绝纠正当前云盘条目；建议换一首验证 |
| HTTP 429/相应限流 code | RateLimited | 请求过于频繁，请稍后再试 |
| DNS、TLS、连接失败 | NetworkError | 网络连接失败 |
| TaskCanceled 且非调用方取消 | Timeout | 请求超时 |
| HTTP 5xx | ServerError | 网易云服务暂时异常 |
| 其他非成功 API code | UnknownApiError | 操作失败，并给出脱敏 CorrelationId |

具体网易云 code 到错误类别的扩展必须基于真实响应夹具或 Probe，不凭记忆补写。

## 6. 重试策略初稿

读请求（Account、Cloud、Cloud by IDs、Song detail）：

- 只对明确的临时网络故障、HTTP 429 和部分 5xx 有限重试；
- 指数退避并尊重 CancellationToken；
- 记录每次尝试的 CorrelationId、endpoint 名、status、API code 和耗时，不记录凭证。

写请求（Match/Cancel）：

- 默认不自动重试；
- 在确定请求没有离开客户端之前才可安全重发；
- 服务端是否已应用但客户端未收到响应时，先刷新状态，不能盲目再次 Match。

## 7. 安全 API Probe 设计

Probe 输入 Cookie 的唯一来源：

1. 环境变量 `NETEASE_MUSIC_COOKIE`；或
2. 运行时隐藏/手工输入（不能进入 shell history、配置文件、源码或 appsettings）。

默认顺序：

1. `GetAccount`
2. `GetCloudSongs(limit: 200, offset: 0)`
3. 用户明确选择一条云盘项后 `GetCloudSongById`
4. 开发者提供公开目标 ID 后 `GetSongDetail`

输出字段：

```text
Operation, CorrelationId, HTTP Status, API code, elapsed milliseconds,
profile userId/nickname（必要时）, cloud count/size/maxSize/page item count,
cloud item existence, target song id/name
```

禁止输出：Cookie、Set-Cookie、Authorization、完整 Header、未经脱敏的原始响应。

Match 不属于默认 Probe。必须通过单独命令/开关显式启用，并再次显示 `userId`、`cloudSongId`、`targetSongId` 的含义和确认。自动测试永远不调用真实 Match。

## 8. 2026-09-12 Probe 记录

### 环境

- `dotnet` SDK：10.0.401
- `NETEASE_MUSIC_COOKIE`：未设置
- Host：`https://music.163.com`
- User-Agent：`NeteaseMusicCloudMatch-Research/1.0`
- 没有发送 Cookie、Authorization 或伪造 IP Header
- 没有调用 Match/Cancel

### 无 Cookie 结果

| Operation | Method | HTTP | API/摘要 | 耗时 |
|---|---|---:|---|---:|
| Account | GET | 200 | `code=200`, `account=null`, `profile=null` | 226 ms |
| Account | POST | 200 | `code=200`, `account=null`, `profile=null` | 48 ms |
| Cloud | GET | 200 | `code=301`, `message=null` | 52 ms |
| Cloud | POST form | 200 | `code=301`, `message=null` | 49 ms |
| Cloud by IDs | GET | 200 | `code=301`, `message=null` | 47 ms |
| Song detail (186137) | GET | 200 | 返回 `id=186137`, `name=双截棍` 的歌曲数据 | 59 ms |

耗时是单次网络观测值，不作为性能基准。

### 当时尚未验证

- 有效完整 Cookie 下 Account POST 是否稳定返回 profile；
- Cloud POST 返回的 2026 实际字段形态和类型漂移；
- Cloud by IDs 在有效会话下的 data 形态；
- 匹配接口是否仍允许 GET、是否需要额外 CSRF/请求头；
- Match 的 `matchData` 当前形态；
- Cancel (`adjustSongId=0`) 当前是否仍成功；
- 是否有任何证据要求切换到 `interface.music.163.com` 或引入加密。

其中读取流程已由 2026-09-13 用户侧日志补充验证；写接口成功仍待复测。

## 9. 2026-09-13 真实账号日志结论

用户提供的脱敏运行日志确认：

- Account POST：HTTP 200 / API 200；
- DPAPI 会话保存成功；
- Cloud POST：HTTP 200 / API 200；
- Song detail GET：HTTP 200 / API 200；
- Cloud by IDs GET：HTTP 200 / API 200；
- Match GET 在 `music.163.com` 三次均为 HTTP 200 / API 400 / `当前歌曲不支持匹配`。

由此可排除 Cookie 失效、账号 UID 未取得、云盘 ID 完全不存在、目标歌曲详情不存在以及基础网络故障。参数方向仍由旧 Windows 源码和自动测试双重固定。当前版本将 Match/Cancel 的 host 改为原始抓包记录使用的 `interface.music.163.com`，但在用户复测前不宣称写入已经成功。

### Probe 与自动测试状态

已创建 `tools/NeteaseMusicCloudMatch.ApiProbe`。默认执行 Account → Cloud List → Cloud by ID → Song Detail；Cookie 只读取 `NETEASE_MUSIC_COOKIE`，缺失时安全退出且不回显值。Match/Cancel 只有在显式传入写操作开关、云盘 ID 和 `--confirm-write=I_UNDERSTAND` 时才可能执行。

本次环境仍未设置 `NETEASE_MUSIC_COOKIE`，因此运行结果是安全停止，未产生认证 API 请求。没有调用真实 Match/Cancel。认证读取和写操作状态仍为“待人工验证”。

Mock `HttpMessageHandler` 自动测试已经固定以下契约：

- Account 使用 POST；
- Cloud 使用 POST form，`limit=200&offset=...`；
- Cloud 单条异常和字段缺失不会让整页失败；
- Song Detail 兼容 `artists`/`album`，实现也兼容 `ar`/`al`；
- API `code=301` 映射 CookieExpired；
- Match 请求中 `songId=cloudSongId`、`adjustSongId=targetSongId`；
- Match/Cancel 使用 `interface.music.163.com`，其他请求使用 `music.163.com`；
- Cancel 产生 `adjustSongId=0`；
- Match 写请求遇到 503 不自动重试；
- API 错误消息中的凭证字段会脱敏。

## 10. 源码证据索引

旧 Windows：

- `Form1.LoadUIDName()`：Account GET 和 profile 字段。
- `Form1.LoadCloudInfo()`：Cloud GET `limit=0` 和容量字段。
- `Form1.GetCloudData()`：Cloud GET `limit/offset` 与列表字段。
- `Form1.CheckCloudFileStatus()`：Cloud by IDs GET。
- `Form1.CheckSongStatus()`：Song detail GET 与 ID 0 取消确认。
- `Form1.button3_Click()`：Match GET、三个参数语义、code 200。
- `CommonHelper.GetHtml()`：所有上述调用实际为 GET、完整 Cookie Header 输入和随机 IP 头。
- `HttpHelper.SetCookie()`：Cookie Header 写入。
- `HttpHelper.GetData()`：Set-Cookie 字符串读取。

macOS（commit `05a36e6`）：

- `Manager/LoginManager.swift`：`getUserInfo()`、`loginWithCookie(_:)`。
- `Manager/NetworkManager.swift`：`request(...)`、GET query、POST form、Cookie Header 和不安全 Debug 输出。
- `Manager/UserManager.swift`：token 随 UserInfo 写入 UserDefaults。
- `Manager/SongManager.swift`：`fetchPage(...)`、`matchCloudSong(...)`、code 301、matchData。
- `Models/Song.swift`：Cloud data 字段映射。
- `Views/LoginView.swift`：完整 Cookie 手工输入流程。
- `Views/SongListView.swift`：本地搜索、分页、直接编辑 ID 并回车匹配。

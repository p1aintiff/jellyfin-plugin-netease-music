# Jellyfin 网易云音乐插件

用于把网易云音乐歌单导入 Jellyfin 音乐库。

## 功能

- 通过网易云歌单 URL 抓取歌单信息。
- 通过 `trackIds` 批量补全歌曲详情。
- 按歌名搜索 Jellyfin 音乐库。
- 按艺人做基础匹配。
- 创建 Jellyfin 歌单。
- Jellyfin 管理界面导入操作页。
- 使用 Jellyfin 日志输出排查链路。

## 编译打包

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1
```

产物：

- `dist\NetEaseMusicImporter\`
- `dist\NetEaseMusicImporter-0.1.4.zip`

## 手动安装

1. 停止 Jellyfin。
2. 解压 `dist\NetEaseMusicImporter-0.1.4.zip`。
3. 把解压后的文件放入 Jellyfin 插件目录：

Windows：

```text
%ProgramData%\Jellyfin\Server\plugins\NetEaseMusicImporter\
```

Linux：

```text
/var/lib/jellyfin/plugins/NetEaseMusicImporter/
```

Docker：

```text
/config/plugins/NetEaseMusicImporter/
```

目录内应包含：

```text
Jellyfin.Plugin.NetEaseMusic.dll
Jellyfin.Plugin.NetEaseMusic.pdb
build.yaml
```

4. 启动 Jellyfin。
5. 在 Jellyfin 日志中确认 `NetEase Music Importer` 已加载。

## 页面使用

进入 Jellyfin 管理后台，打开 `NetEase Music` 页面：

1. 确认页面显示 `Page version: 0.1.4` 和当前用户。
2. 输入网易云歌单 URL。
3. 可选填写 Jellyfin 歌单名。
4. 选择 `Private playlist` 或 `Public playlist`。
5. 点击 `Import playlist`。
6. 页面会显示匹配数量、未匹配数量和 `operationId`。

## API

需要使用 Jellyfin API Token：

```http
Authorization: MediaBrowser Token="你的 API Token"
```

导入网易云歌单：

```powershell
$headers = @{ Authorization = 'MediaBrowser Token="你的 API Token"' }
$body = @{
  Url = "https://music.163.com/m/playlist?id=13822175569"
  PlaylistName = "网易云歌单"
  Public = $false
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8096/NetEaseMusic/Import" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

其他接口：

- `GET /NetEaseMusic/CurrentUser`

## 日志排查

API 响应会返回 `operationId`。在 Jellyfin 日志中搜索该值，可定位同一次请求的抓取、匹配和创建日志。

## 当前限制

- 网易云抓取只保留 API 路径，不解析 HTML 页面。
- 歌曲匹配只做歌名搜索和艺人匹配。
- 需要在真实 Jellyfin 实例中验证歌单持久化行为。

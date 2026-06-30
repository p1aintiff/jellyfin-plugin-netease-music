# Jellyfin 网易云音乐歌单导入插件

把网易云音乐歌单导入 Jellyfin 音乐库。

## 功能

- 通过网易云歌单 URL 获取歌单信息。
- 通过 `trackIds` 批量补全歌曲详情。
- 按歌名搜索 Jellyfin 音乐库。
- 按艺人做基础匹配。
- 创建 Jellyfin 歌单并添加匹配到的歌曲。
- 提供 Jellyfin 管理后台导入页面。

## 在线安装

1. 打开 Jellyfin 管理后台。
2. 进入 `插件` -> `存储库`。
3. 添加插件仓库地址：

```text
https://p1aintiff.github.io/jellyfin-plugin-netease-music/manifest.json
```

4. 进入 `目录`，安装 `NetEase Music Importer`。
5. 重启 Jellyfin。

## 手动安装

构建插件包：

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1
```

将 `dist\NetEaseMusicImporter-0.1.5.zip` 解压到 Jellyfin 插件目录。

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

插件目录内应包含：

```text
Jellyfin.Plugin.NetEaseMusic.dll
Jellyfin.Plugin.NetEaseMusic.pdb
build.yaml
```

## 使用

1. 打开 Jellyfin 管理后台。
2. 打开 `NetEase Music` 页面。
3. 输入网易云歌单 URL。
4. 可选填写 Jellyfin 歌单名。
5. 选择是否公开歌单。
6. 点击 `Import playlist`。

## API

接口需要 Jellyfin Token。

```http
Authorization: MediaBrowser Token="你的 API Token"
```

导入歌单：

```powershell
$headers = @{ Authorization = 'MediaBrowser Token="你的 API Token"' }
$body = @{
  Url = "https://music.163.com/m/playlist?id=13822175569"
  PlaylistName = "网易云歌单"
  Public = $true
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8096/NetEaseMusic/Import" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

获取当前用户：

```text
GET /NetEaseMusic/CurrentUser
```

## 开发

```powershell
dotnet build .\JellyfinMusic.slnx
powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1
```

## 自动编译

仓库已配置 GitHub Actions：

- 推送到 `main` 会自动构建插件。
- 在 GitHub 页面进入 `Actions` -> `Build plugin repository` -> `Run workflow` 可手动触发。
- 构建成功后会发布 GitHub Pages。
- Jellyfin 在线安装地址为：

```text
https://p1aintiff.github.io/jellyfin-plugin-netease-music/manifest.json
```

## 说明

- 当前版本：`0.1.5`
- 目标 Jellyfin ABI：`10.10.7.0`
- 网易云抓取只使用 API 路径。
- 歌曲匹配策略保持简单：歌名搜索 + 艺人匹配。

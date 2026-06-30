# Jellyfin 网易云音乐歌单导入插件

把网易云音乐歌单导入 Jellyfin 音乐库。

## 功能

- 通过网易云歌单 URL 获取歌单信息。
- 通过 `trackIds` 批量补全歌曲详情。
- 按歌名搜索 Jellyfin 音乐库。
- 按艺人做基础匹配。
- 创建 Jellyfin 歌单并添加匹配到的歌曲。
- 保存导入历史，并可按历史记录更新已创建的 Jellyfin 歌单。
- 可单独删除导入历史，不删除 Jellyfin 歌单。
- 提供 Jellyfin 管理后台操作页面。

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

将 `dist\NetEaseMusicImporter-0.2.1.zip` 解压到 Jellyfin 插件目录。

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
6. 选择是否保存导入历史，默认保存。
7. 点击 `Import playlist`。

## 导入历史

- 导入历史显示已创建的歌单名称和网易云歌单链接。
- 点击 `Update` 会按网易云歌单当前内容同步 Jellyfin 歌单。
- 点击 `Delete cache` 只删除导入历史，不删除 Jellyfin 歌单。

## API

以下仅用于手动调试接口。正常在 Jellyfin 管理后台页面使用时，不需要手动填写 Token。

手动调用接口需要 Jellyfin Token：

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
  SaveCache = $true
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8096/NetEaseMusic/Import" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

获取导入历史：

```text
GET /NetEaseMusic/Imports
```

更新历史歌单：

```text
POST /NetEaseMusic/Imports/{playlistId}/Refresh
```

删除导入历史：

```text
DELETE /NetEaseMusic/Imports/{playlistId}
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

## 版本信息

- 插件仓库 manifest 的基础描述和版本更新说明维护在 `manifest-info.json`。
- 发布新版本时，同步更新 `manifest-info.json` 中对应版本的 changelog。
- GitHub Actions 会按 `manifest-info.json` 中存在对应 `v版本号` tag 的版本生成历史包，并为当前版本生成新的包地址和校验值。

## 说明

- 当前版本：`0.2.1`
- 目标 Jellyfin ABI：`10.10.7.0`
- 网易云抓取只使用 API 路径。
- 歌曲匹配策略保持简单：歌名搜索 + 艺人匹配。

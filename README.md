# Jellyfin 网易云音乐歌单导入插件

把网易云音乐歌单导入 Jellyfin 音乐库。

## 功能

- 通过网易云歌单 URL 获取歌单信息。
- 通过 `trackIds` 批量补全歌曲详情。
- 按歌名搜索 Jellyfin 音乐库。
- 歌名规范化后相等，且至少一个完整艺人名称相等时匹配。
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

从 [GitHub Releases](https://github.com/p1aintiff/jellyfin-plugin-netease-music/releases) 下载插件包，或使用本地构建的 `dist\NetEaseMusicImporter-0.2.4.zip`，解压到 Jellyfin 插件目录。

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
7. 点击 `开始导入`，在“任务状态与结果”中查看匹配统计和未匹配歌曲。

插件只匹配 Jellyfin 音乐库中已有的歌曲，不会下载音源。导入前请完成音乐库扫描，并尽量补全歌名和艺人信息。

## 导入历史

- 导入历史显示已创建的歌单名称和网易云歌单链接。
- 点击 `更新歌单` 会按网易云歌单当前内容重新匹配，并替换 Jellyfin 歌单中的歌曲。
- 点击 `删除记录` 只删除导入历史，不删除 Jellyfin 歌单。
- 点击 `刷新列表` 可重新加载导入历史。

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

## 发布版本

仓库已配置 GitHub Actions：

- 推送 `v版本号` 标签（如 `v0.2.4`）会构建插件并创建 GitHub Release。
- Release 保存插件 ZIP、版本说明和插件目录快照 `manifest.json`；GitHub Pages 只托管在线安装目录。
- 普通 `main` 提交不再发布，避免相同版本号对应不同安装包。
- 需要重试时，在 `Actions` -> `Release plugin` -> `Run workflow` 中选择对应版本标签。已发布的 Release 会直接复用目录快照，不会重新构建或覆盖安装包。
- 首次切换发布方式时，将现有 Pages 历史安装包原样迁移到该次 Release 附件中，保留历史版本和校验值。
- Jellyfin 在线安装地址为：

```text
https://p1aintiff.github.io/jellyfin-plugin-netease-music/manifest.json
```

## 版本信息

- 插件仓库 manifest 的基础描述和版本更新说明维护在 `manifest-info.json`。
- 发布新版本时，同步更新项目的 `Version`、`AssemblyVersion`、`FileVersion`，以及 `build.yaml` 的版本和 `manifest-info.json` 的 changelog。
- 提交并推送 `main` 后，创建与项目版本一致的标签并推送：

```powershell
git tag v0.2.4
git push origin v0.2.4
```

- GitHub Actions 会校验版本一致性；新目录沿用已发布历史版本的下载地址，不会重新构建历史安装包。

## 说明

- 当前版本：`0.2.4`
- 目标 Jellyfin ABI：`10.10.7.0`
- 网易云抓取只使用 API 路径。
- 歌曲匹配策略保持简单：按歌名搜索最多 30 个候选，再要求歌名相等且至少一个完整艺人名称相等；比较时统一大小写、全半角并忽略空白。
- 保留 Live、Remix、伴奏等版本文字及标点，不使用相似度评分或阈值；多个候选满足条件时使用第一个。

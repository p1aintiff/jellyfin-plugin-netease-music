# Jellyfin NetEase Music Importer

Import NetEase Cloud Music playlists into a Jellyfin music library.

## Features

- Fetch NetEase playlist metadata by playlist URL.
- Complete track details through NetEase `trackIds`.
- Match Jellyfin audio items by song title and artist.
- Create a Jellyfin playlist and add matched songs.
- Provide a Jellyfin dashboard import page.

## Online Install

1. Open Jellyfin dashboard.
2. Go to `Plugins` -> `Repositories`.
3. Add this repository URL:

```text
https://<github-user>.github.io/<repo>/manifest.json
```

4. Open `Catalog`, install `NetEase Music Importer`, then restart Jellyfin.

## Manual Install

Build the package:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1
```

Install `dist\NetEaseMusicImporter-0.1.5.zip` into the Jellyfin plugin directory.

Windows:

```text
%ProgramData%\Jellyfin\Server\plugins\NetEaseMusicImporter\
```

Linux:

```text
/var/lib/jellyfin/plugins/NetEaseMusicImporter/
```

Docker:

```text
/config/plugins/NetEaseMusicImporter/
```

The plugin directory should contain:

```text
Jellyfin.Plugin.NetEaseMusic.dll
Jellyfin.Plugin.NetEaseMusic.pdb
build.yaml
```

## Usage

1. Open Jellyfin dashboard.
2. Open `NetEase Music`.
3. Enter a NetEase playlist URL.
4. Optionally enter a Jellyfin playlist name.
5. Choose whether the playlist is public.
6. Click `Import playlist`.

## API

The API requires a Jellyfin token.

```http
Authorization: MediaBrowser Token="your API token"
```

Import a playlist:

```powershell
$headers = @{ Authorization = 'MediaBrowser Token="your API token"' }
$body = @{
  Url = "https://music.163.com/m/playlist?id=13822175569"
  PlaylistName = "NetEase Playlist"
  Public = $true
} | ConvertTo-Json

Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:8096/NetEaseMusic/Import" `
  -Headers $headers `
  -ContentType "application/json" `
  -Body $body
```

Current user check:

```text
GET /NetEaseMusic/CurrentUser
```

## Development

```powershell
dotnet build .\JellyfinMusic.slnx
powershell -ExecutionPolicy Bypass -File .\scripts\package-plugin.ps1
```

## Notes

- Current version: `0.1.5`
- Target Jellyfin ABI: `10.10.7.0`
- The NetEase scraper uses API endpoints only.
- Song matching is intentionally simple: title search plus artist match.

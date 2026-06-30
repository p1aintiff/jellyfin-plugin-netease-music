$ErrorActionPreference = "Stop"

$root = Resolve-Path (Join-Path $PSScriptRoot "..")
$project = Join-Path $root "Jellyfin.Plugin.NetEaseMusic/Jellyfin.Plugin.NetEaseMusic.csproj"
$publish = Join-Path $root "Jellyfin.Plugin.NetEaseMusic/bin/Release/net8.0/publish"
$dist = Join-Path $root "dist"
$packageDir = Join-Path $dist "NetEaseMusicImporter"
$projectXml = [xml](Get-Content $project)
$version = $projectXml.Project.PropertyGroup.Version
$zip = Join-Path $dist "NetEaseMusicImporter-$version.zip"

dotnet publish $project -c Release

if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}
New-Item -ItemType Directory -Path $packageDir | Out-Null

Copy-Item (Join-Path $publish "Jellyfin.Plugin.NetEaseMusic.dll") $packageDir
Copy-Item (Join-Path $publish "Jellyfin.Plugin.NetEaseMusic.pdb") $packageDir
Copy-Item (Join-Path $root "build.yaml") $packageDir

if (Test-Path $zip) {
    Remove-Item $zip -Force
}
Compress-Archive -Path (Join-Path $packageDir "*") -DestinationPath $zip

Write-Host "Package created:"
Write-Host $packageDir
Write-Host $zip

param(
    [Parameter(Mandatory)] [string] $Repository,
    [Parameter(Mandatory)] [string] $Tag,
    [string] $PreviousManifestUrl
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = [xml](Get-Content (Join-Path $root 'Jellyfin.Plugin.NetEaseMusic/Jellyfin.Plugin.NetEaseMusic.csproj'))
$info = Get-Content (Join-Path $root 'manifest-info.json') | ConvertFrom-Json
$build = Get-Content (Join-Path $root 'build.yaml') -Raw
$version = $project.Project.PropertyGroup.Version
$assemblyVersion = $project.Project.PropertyGroup.AssemblyVersion
if ($Tag -ne "v$version") { throw "Expected tag v$version." }
$targetAbi = [regex]::Match($build, '(?m)^targetAbi: "([^"]+)"').Groups[1].Value
$owner, $repoName = $Repository.Split('/')
$pagesUrl = "https://$owner.github.io/$repoName"
$releaseUrl = "https://github.com/$Repository/releases/download/$Tag"
$dist = Join-Path $root 'dist'
$site = Join-Path $root 'site'
$zipName = "NetEaseMusicImporter-$version.zip"
$zip = Join-Path $dist $zipName
if (-not $PreviousManifestUrl) { $PreviousManifestUrl = "$pagesUrl/manifest.json" }
$previous = Invoke-RestMethod $PreviousManifestUrl
$versions = @(
    [ordered]@{
        version = $assemblyVersion
        changelog = $info.changelog.$assemblyVersion
        targetAbi = $targetAbi
        sourceUrl = "$releaseUrl/$zipName"
        checksum = (Get-FileHash $zip -Algorithm MD5).Hash.ToLowerInvariant()
        timestamp = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    }
)

# Migrate existing Pages packages once, preserving their exact bytes and checksums.
foreach ($entry in $previous.versions) {
    if ($entry.version -eq $assemblyVersion) { continue }
    if ($entry.sourceUrl.StartsWith("$pagesUrl/", [StringComparison]::Ordinal)) {
        $legacyName = [IO.Path]::GetFileName(([uri]$entry.sourceUrl).AbsolutePath)
        $legacyZip = Join-Path $dist $legacyName
        Invoke-WebRequest $entry.sourceUrl -OutFile $legacyZip
        if ((Get-FileHash $legacyZip -Algorithm MD5).Hash.ToLowerInvariant() -ne $entry.checksum) {
            throw "Checksum mismatch for $legacyName."
        }
        $entry.sourceUrl = "$releaseUrl/$legacyName"
    }
    $versions += $entry
}

$manifest = @(
    [ordered]@{
        guid = $info.guid
        name = $info.name
        description = $info.description
        overview = $info.overview
        imageUrl = $info.imageUrl
        owner = $owner
        category = $info.category
        versions = @($versions | Sort-Object { [version]$_.version } -Descending)
    }
)
New-Item -ItemType Directory -Force -Path $site | Out-Null
ConvertTo-Json -InputObject $manifest -Depth 8 | Set-Content (Join-Path $site 'manifest.json') -Encoding utf8
$info.changelog.$assemblyVersion | Set-Content (Join-Path $dist 'release-notes.md') -Encoding utf8
Write-Host "Plugin repository prepared for $Tag."

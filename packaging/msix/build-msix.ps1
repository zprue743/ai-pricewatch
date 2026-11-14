param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$PublishDir,

    [Parameter(Mandatory = $true)]
    [string]$OutputPath,

    [string]$IdentityName = "zprue743.AiPriceWatch",
    [string]$Publisher = "CN=zprue743",
    [string]$PublisherDisplayName = "AiPriceWatch Contributors"
)

$ErrorActionPreference = "Stop"

# Normalize SemVer (e.g. 0.2.0 or 0.2.0-beta.1) to 4-part MSIX version X.Y.Z.0
$coreVersion = ($Version -split '[-+]')[0]
$parts = $coreVersion.Split('.')
while ($parts.Count -lt 3) { $parts += '0' }
$msixVersion = "$($parts[0]).$($parts[1]).$($parts[2]).0"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$templatePath = Join-Path $scriptDir "AppxManifest.template.xml"
$assetsSource = Join-Path $scriptDir "Assets"

$stagingDir = Join-Path ([System.IO.Path]::GetTempPath()) "aipw-msix-stage-$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory -Force -Path $stagingDir | Out-Null

try {
    Copy-Item -Path (Join-Path $PublishDir "*") -Destination $stagingDir -Recurse -Force
    $stagingAssets = Join-Path $stagingDir "Assets"
    New-Item -ItemType Directory -Force -Path $stagingAssets | Out-Null
    Copy-Item -Path (Join-Path $assetsSource "*") -Destination $stagingAssets -Force

    $manifest = Get-Content -Path $templatePath -Raw -Encoding UTF8
    $manifest = $manifest.Replace("__IDENTITY_NAME__", $IdentityName)
    $manifest = $manifest.Replace("__PUBLISHER__", $Publisher)
    $manifest = $manifest.Replace("__PUBLISHER_DISPLAY_NAME__", $PublisherDisplayName)
    $manifest = $manifest.Replace("__MSIX_VERSION__", $msixVersion)

    $manifestOut = Join-Path $stagingDir "AppxManifest.xml"
    Set-Content -Path $manifestOut -Value $manifest -Encoding UTF8

    $makeAppx = Get-Command "makeappx.exe" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
    if (-not $makeAppx) {
        $sdkBin = Get-ChildItem "C:\Program Files (x86)\Windows Kits\10\bin" -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\makeappx.exe" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1
        $makeAppx = $sdkBin
    }

    if (-not $makeAppx -or -not (Test-Path $makeAppx)) {
        throw "makeappx.exe not found. Install Windows 10/11 SDK."
    }

    $outDir = Split-Path -Parent $OutputPath
    if ($outDir) {
        New-Item -ItemType Directory -Force -Path $outDir | Out-Null
    }

    & $makeAppx pack /d $stagingDir /p $OutputPath /o
    if ($LASTEXITCODE -ne 0) {
        throw "makeappx.exe failed with exit code $LASTEXITCODE"
    }

    Write-Host "Built Store-compatible MSIX package: $OutputPath (version $msixVersion)"
}
finally {
    if (Test-Path $stagingDir) {
        Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

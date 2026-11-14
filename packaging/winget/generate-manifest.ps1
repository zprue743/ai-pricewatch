param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [Parameter(Mandatory = $true)]
    [string]$InstallerSha256,

    [string]$OutputDir = "out/winget"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$targetDir = Join-Path $OutputDir "manifests/z/zprue743/AiPriceWatch/$Version"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null

$shaUpper = $InstallerSha256.Trim().ToUpperInvariant()
if ($shaUpper -notmatch '^[A-F0-9]{64}$') {
    throw "Invalid SHA-256 hash: $InstallerSha256"
}

$files = @(
    "zprue743.AiPriceWatch.yaml",
    "zprue743.AiPriceWatch.installer.yaml",
    "zprue743.AiPriceWatch.locale.en-US.yaml"
)

foreach ($file in $files) {
    $content = Get-Content (Join-Path $scriptDir $file) -Raw -Encoding UTF8
    $content = $content.Replace("__VERSION__", $Version).Replace("__SHA256__", $shaUpper)
    Set-Content -Path (Join-Path $targetDir $file) -Value $content -Encoding UTF8
}

Write-Host "Generated WinGet manifest in $targetDir"
if (Get-Command winget -ErrorAction SilentlyContinue) {
    winget validate --manifest $targetDir
}

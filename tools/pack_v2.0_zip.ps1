# ============================================================================
# 脚本名称: pack_v2.0_zip.ps1
# 所属区域: tools
# 职责: 为 Gitee / GitHub Releases 规范打包 FerrisPulse_v{Version}_Portable.zip
# ============================================================================

param(
    [string]$Version = "2.0.1"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Get-Item "$scriptDir\..").FullName

$cleanVer = $Version.TrimStart('v', 'V')
$tempStage = Join-Path ([System.IO.Path]::GetTempPath()) ("FP_Pack_" + [System.Guid]::NewGuid().ToString("N"))
if (Test-Path $tempStage) { Remove-Item $tempStage -Recurse -Force }
New-Item -ItemType Directory -Path $tempStage | Out-Null

try {
    Copy-Item (Join-Path $rootDir "FerrisPulse.exe") (Join-Path $tempStage "FerrisPulse.exe")
    Copy-Item (Join-Path $rootDir "README.md") (Join-Path $tempStage "README.md")
    Copy-Item (Join-Path $rootDir "LICENSE") (Join-Path $tempStage "LICENSE")
    Copy-Item (Join-Path $rootDir "CHANGELOG.md") (Join-Path $tempStage "CHANGELOG.md")

    $legacyLog = Join-Path $rootDir "docs\CHANGELOG_LEGACY.txt"
    if (Test-Path $legacyLog) {
        Copy-Item $legacyLog (Join-Path $tempStage "更新日志.txt")
    }

    $faq = Join-Path $rootDir "docs\FAQ.txt"
    if (Test-Path $faq) {
        Copy-Item $faq (Join-Path $tempStage "使用说明与常见问题.txt")
    }

    $zipPath = Join-Path $rootDir "FerrisPulse_v${cleanVer}_Portable.zip"
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempStage, $zipPath, [System.IO.Compression.CompressionLevel]::Optimal, $false)

    $item = Get-Item $zipPath
    Write-Host "[SUCCESS] Zip generated: $($item.FullName) - $($item.Length) bytes" -ForegroundColor Green
    [System.IO.Compression.ZipFile]::OpenRead($zipPath).Entries | Select-Object FullName, Length
}
finally {
    if (Test-Path $tempStage) { Remove-Item $tempStage -Recurse -Force -ErrorAction SilentlyContinue }
}

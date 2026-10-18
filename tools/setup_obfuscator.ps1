# ============================================================================
# 脚本名称: setup_obfuscator.ps1
# 核心职责:
#   自动检测并就绪 Obfuscar 代码混淆引擎工具链，支持离线缓存与全自动下载
# ============================================================================

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$obfuscarDir = Join-Path $scriptDir "obfuscar"
$targetExe = Join-Path $obfuscarDir "Obfuscar.Console.exe"

if (Test-Path $targetExe) {
    Write-Host "[OK] Obfuscar.Console.exe already exists: $targetExe" -ForegroundColor Green
    exit 0
}

if (-not (Test-Path $obfuscarDir)) {
    New-Item -ItemType Directory -Path $obfuscarDir -Force | Out-Null
}

Write-Host "Obfuscar.Console.exe not found. Downloading from NuGet..." -ForegroundColor Yellow

$tempPkg = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "obfuscar_pkg_" + [System.Guid]::NewGuid().ToString("N") + ".zip")

try {
    # 优先从 NuGet 官方获取 2.2.38 (完全兼容 .NET 4.0)
    $nugetUrls = @(
        "https://www.nuget.org/api/v2/package/Obfuscar/2.2.38",
        "https://api.nuget.org/v3-flatcontainer/obfuscar/2.2.38/obfuscar.2.2.38.nupkg"
    )

    $downloaded = $false
    foreach ($url in $nugetUrls) {
        try {
            Write-Host "Fetching: $url"
            Invoke-WebRequest -Uri $url -OutFile $tempPkg -TimeoutSec 15 -UseBasicParsing
            if ((Test-Path $tempPkg) -and (Get-Item $tempPkg).Length -gt 100000) {
                $downloaded = $true
                break
            }
        }
        catch {
            Write-Warning "Failed from $url : $_"
        }
    }

    if (-not $downloaded) {
        Write-Error "Failed to download Obfuscar package from NuGet!"
        exit 1
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip = [System.IO.Compression.ZipFile]::OpenRead($tempPkg)
    try {
        foreach ($entry in $zip.Entries) {
            if ($entry.FullName -like "tools/*" -and -not $entry.FullName.EndsWith("/")) {
                $fileName = [System.IO.Path]::GetFileName($entry.FullName)
                $destPath = Join-Path $obfuscarDir $fileName
                Write-Host "Extracting: $fileName -> $destPath"
                [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
            }
        }
    }
    finally {
        $zip.Dispose()
    }

    if (-not (Test-Path $targetExe)) {
        Write-Error "Extraction failed: Obfuscar.Console.exe not found in extracted files!"
        exit 1
    }

    Write-Host "[SUCCESS] Obfuscar successfully installed to $targetExe" -ForegroundColor Green
}
finally {
    if (Test-Path $tempPkg) {
        Remove-Item $tempPkg -Force -ErrorAction SilentlyContinue
    }
}

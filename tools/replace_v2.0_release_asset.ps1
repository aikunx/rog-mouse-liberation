# ============================================================================
# 脚本名称: replace_v2.0_release_asset.ps1
# 所属区域: tools
# 职责: 将无混淆、无静默写桌面、双经典托盘样式的纯净版更新替换至 Gitee v2.0.0 发布资产
# ============================================================================

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$rootDir = (Get-Item "$scriptDir\..").FullName

# 1. 加载安全隔离的 Token
$envFile = Join-Path $scriptDir ".release_env"
if (Test-Path $envFile) {
    Get-Content $envFile -Encoding UTF8 | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#") -and $line.Contains("=")) {
            $parts = $line.Split("=", 2)
            [Environment]::SetEnvironmentVariable($parts[0].Trim(), $parts[1].Trim(), "Process")
        }
    }
}

$giteeToken = [Environment]::GetEnvironmentVariable("GITEE_TOKEN", "Process")
$giteeRepo = [Environment]::GetEnvironmentVariable("GITEE_REPO", "Process")
$ghToken = [Environment]::GetEnvironmentVariable("GITHUB_TOKEN", "Process")
$ghRepo = [Environment]::GetEnvironmentVariable("GITHUB_REPO", "Process")

if (-not $giteeRepo) { $giteeRepo = "Ferris-echo/ferris-pulse" }
if (-not $ghRepo) { $ghRepo = "Ferris-echo/FerrisPulse" }

$zipPath = Join-Path $rootDir "FerrisPulse_v2.0_Portable.zip"
$exePath = Join-Path $rootDir "FerrisPulse.exe"

if (-not (Test-Path $zipPath) -or -not (Test-Path $exePath)) {
    Write-Error "FAIL: $zipPath or $exePath missing!"
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host " [Gitee v2.0.0 资产热替换] 目标: $giteeRepo / v2.0.0" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 2. 定位 Gitee Release v2.0.0
$releasesUrl = "https://gitee.com/api/v5/repos/$giteeRepo/releases?access_token=$giteeToken"
$allRel = Invoke-RestMethod -Uri $releasesUrl -UserAgent "FerrisPulse-Release"
$v2Rel = $null
foreach ($r in $allRel) {
    if ($r.tag_name -eq "v2.0.0") {
        $v2Rel = $r
        break
    }
}

if ($null -eq $v2Rel) {
    Write-Error "FAIL: Gitee release v2.0.0 not found!"
    exit 1
}

Write-Host "找到 Gitee v2.0.0 Release (ID: $($v2Rel.id))" -ForegroundColor Green

# 3. 删除 Gitee 历史资产
$giteeRelInfo = Invoke-RestMethod -Uri "https://gitee.com/api/v5/repos/$giteeRepo/releases/$($v2Rel.id)?access_token=$giteeToken" -UserAgent "FerrisPulse-Release"
foreach ($ga in $giteeRelInfo.assets) {
    if ($ga.name -eq "FerrisPulse_v2.0_Portable.zip" -or $ga.name -eq "FerrisPulse.exe") {
        Write-Host "  -> [Gitee] 删除旧版本资产: $($ga.name) (ID: $($ga.id))..." -ForegroundColor Yellow
        try {
            Invoke-RestMethod -Uri "https://gitee.com/api/v5/repos/$giteeRepo/releases/$($v2Rel.id)/attach_files/$($ga.id)?access_token=$giteeToken" -Method Delete -UserAgent "FerrisPulse-Release" | Out-Null
        } catch {
            Write-Warning "删除资产 $($ga.name) 异常: $_"
        }
    }
}

Start-Sleep -Seconds 1

# 4. 上传全新纯净无混淆资产到 Gitee
Write-Host "  -> [Gitee] 上传全新纯净版 FerrisPulse_v2.0_Portable.zip..." -ForegroundColor Cyan
& curl.exe -s -X POST "https://gitee.com/api/v5/repos/$giteeRepo/releases/$($v2Rel.id)/attach_files" -F "access_token=$giteeToken" -F "file=@$zipPath" | Out-Null

Write-Host "  -> [Gitee] 上传全新纯净版 FerrisPulse.exe..." -ForegroundColor Cyan
& curl.exe -s -X POST "https://gitee.com/api/v5/repos/$giteeRepo/releases/$($v2Rel.id)/attach_files" -F "access_token=$giteeToken" -F "file=@$exePath" | Out-Null

Write-Host "  -> [Gitee] 资产上传完成！" -ForegroundColor Green

# 5. 同步更新 GitHub v2.0.0 (如果存在)
if ($ghToken) {
    Write-Host "`n==========================================================" -ForegroundColor Cyan
    Write-Host " [GitHub v2.0.0 资产同步热替换] 目标: $ghRepo / v2.0.0" -ForegroundColor Cyan
    Write-Host "==========================================================" -ForegroundColor Cyan
    $ghHeaders = @{
        "Authorization" = "Bearer $ghToken"
        "Accept" = "application/vnd.github.v3+json"
        "User-Agent" = "FerrisPulse-Release"
    }

    try {
        $ghRel = Invoke-RestMethod -Uri "https://api.github.com/repos/$ghRepo/releases/tags/v2.0.0" -Headers $ghHeaders
        if ($ghRel) {
            Write-Host "找到 GitHub v2.0.0 Release (ID: $($ghRel.id))" -ForegroundColor Green
            $ghAssets = Invoke-RestMethod -Uri "https://api.github.com/repos/$ghRepo/releases/$($ghRel.id)/assets" -Headers $ghHeaders
            foreach ($ea in $ghAssets) {
                if ($ea.name -eq "FerrisPulse.exe" -or $ea.name -eq "FerrisPulse_v2.0_Portable.zip" -or $ea.name -eq "FerrisPulse_v2.0.0_Portable.zip") {
                    Write-Host "  -> [GitHub] 删除旧版本资产: $($ea.name) (ID: $($ea.id))..." -ForegroundColor Yellow
                    Invoke-RestMethod -Uri "https://api.github.com/repos/$ghRepo/releases/assets/$($ea.id)" -Method Delete -Headers $ghHeaders | Out-Null
                }
            }

            Start-Sleep -Seconds 1

            Write-Host "  -> [GitHub] 上传 FerrisPulse_v2.0_Portable.zip..." -ForegroundColor Cyan
            & curl.exe -s -X POST "https://uploads.github.com/repos/$ghRepo/releases/$($ghRel.id)/assets?name=FerrisPulse_v2.0_Portable.zip" -H "Authorization: Bearer $ghToken" -H "Content-Type: application/octet-stream" --data-binary "@$zipPath" | Out-Null

            Write-Host "  -> [GitHub] 上传 FerrisPulse.exe..." -ForegroundColor Cyan
            & curl.exe -s -X POST "https://uploads.github.com/repos/$ghRepo/releases/$($ghRel.id)/assets?name=FerrisPulse.exe" -H "Authorization: Bearer $ghToken" -H "Content-Type: application/octet-stream" --data-binary "@$exePath" | Out-Null

            Write-Host "  -> [GitHub] 资产上传完成！" -ForegroundColor Green
        }
    } catch {
        Write-Warning "GitHub v2.0.0 资产替换跳过或异常: $_"
    }
}

# 6. 验证 Gitee 直链可下载性
Write-Host "`n验证 Gitee 目标链接是否就绪..." -ForegroundColor Cyan
$targetUrl = "https://gitee.com/$giteeRepo/releases/download/v2.0.0/FerrisPulse_v2.0_Portable.zip"
$verifyCode = & curl.exe -s -o /dev/null -w "%{http_code}" -L "$targetUrl"
Write-Host "HTTP 响应码: $verifyCode (预期 200)" -ForegroundColor Green

if ($verifyCode -eq "200") {
    Write-Host "`n[SUCCESS] 目标下载地址已成功替换为全新纯净版：" -ForegroundColor Green
    Write-Host "$targetUrl" -ForegroundColor Cyan
} else {
    Write-Warning "HTTP 状态码为 $verifyCode，请稍后再次验证 CDN 同步状态。"
}

# ============================================================================
# 测试用例: test_obfuscation_integrity.ps1
# 验证内容:
#   开源发布纯净二进制契约：
#   1. build.bat 遵从开源策略，默认发布纯净标准的 Raw Binary（停用 Obfuscar 混淆）
#   2. 产物 FerrisPulse.exe 具备完整的 PE 元数据与清晰的程序集符号，彻底规避杀软启发式误报
# ============================================================================

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = (Get-Item "$scriptDir\..").FullName

# 1. 验证 build.bat 停用混淆并直出纯净二进制
$buildBat = Join-Path $repoRoot "build.bat"
$batContent = Get-Content -Path $buildBat -Raw -Encoding UTF8
if ($batContent -match "tools\\obfuscar\\Obfuscar\.Console\.exe") {
    Write-Error "FAIL: build.bat should not invoke Obfuscar in open-source pure binary mode!"
    exit 1
}
Write-Host "PASS: build.bat produces clean, unobfuscated Raw Binary for open-source release." -ForegroundColor Green

# 2. 验证 FerrisPulse.exe 具备清晰完整的程序集信息与入口
$exePath = Join-Path $repoRoot "FerrisPulse.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "FAIL: FerrisPulse.exe does not exist!"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($exePath)
$asm = [System.Reflection.Assembly]::Load($bytes)
$progType = $asm.GetType("RazerBatteryTray.Program")
if ($null -eq $progType) {
    Write-Error "FAIL: Type RazerBatteryTray.Program not found in clean binary!"
    exit 1
}
$mainFormType = $asm.GetType("RazerBatteryTray.MainForm")
if ($null -eq $mainFormType) {
    Write-Error "FAIL: Type RazerBatteryTray.MainForm not found in clean binary!"
    exit 1
}

Write-Host "PASS: FerrisPulse.exe contains clean, uncorrupted .NET assembly metadata." -ForegroundColor Green
Write-Host "ALL TESTS PASSED for test_obfuscation_integrity.ps1" -ForegroundColor Cyan

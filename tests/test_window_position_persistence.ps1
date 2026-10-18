# tests\test_window_position_persistence.ps1
# 验证主窗体启动时位置记忆能够准确从注册表恢复，且不会被居中坐标覆盖

$ErrorActionPreference = "Stop"

$regPath = "HKCU:\Software\FerrisPulse"
if (-not (Test-Path $regPath)) {
    New-Item -Path $regPath -Force | Out-Null
}

$expectedX = 1420
$expectedY = 260

# 1. 预先向注册表写入特定坐标
Set-ItemProperty -Path $regPath -Name "WindowX" -Value $expectedX -Type DWord
Set-ItemProperty -Path $regPath -Name "WindowY" -Value $expectedY -Type DWord

Write-Host "Injected test coordinates into registry: ($expectedX, $expectedY)"

# 2. 反射加载并实例化 MainForm
$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) { $asmPath = "FerrisPulse.exe" }
$fullPath = (Resolve-Path $asmPath).Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tMainForm = $asm.GetType("RazerBatteryTray.MainForm")
if ($tMainForm -eq $null) {
    Write-Host "FAIL: Type RazerBatteryTray.MainForm not found" -ForegroundColor Red
    exit 1
}

# 实例化 MainForm(false) - 非自启动模式
$form = [Activator]::CreateInstance($tMainForm, @($false))
[System.Windows.Forms.Form]$winForm = $form

# 触发句柄与窗口加载
$handle = $winForm.Handle
$winForm.Visible = $true

# 3. 校验窗口坐标与注册表坐标
$actualX = $winForm.Location.X
$actualY = $winForm.Location.Y

$regX = (Get-ItemProperty -Path $regPath).WindowX
$regY = (Get-ItemProperty -Path $regPath).WindowY

Write-Host "Form Location: ($actualX, $actualY)"
Write-Host "Registry Location: ($regX, $regY)"

$failed = $false

# 检查窗口坐标是否准确恢复
if ($actualX -ne $expectedX -or $actualY -ne $expectedY) {
    Write-Host "FAIL: Form location ($actualX, $actualY) does not match expected ($expectedX, $expectedY)" -ForegroundColor Red
    $failed = $true
} else {
    Write-Host "PASS: Form location matches expected ($expectedX, $expectedY)" -ForegroundColor Green
}

# 检查注册表中的坐标是否被错误覆写
if ($regX -ne $expectedX -or $regY -ne $expectedY) {
    Write-Host "FAIL: Registry coordinates ($regX, $regY) were overwritten during form startup!" -ForegroundColor Red
    $failed = $true
} else {
    Write-Host "PASS: Registry coordinates were preserved" -ForegroundColor Green
}

# 现场清理与关闭
$winForm.Dispose()

if ($failed) {
    Write-Host "Test failed as expected in RED phase (window position restoration missing in OnLoad/startup)." -ForegroundColor Yellow
    exit 1
}

Write-Host "ALL TESTS PASSED for test_window_position_persistence.ps1" -ForegroundColor Green
exit 0

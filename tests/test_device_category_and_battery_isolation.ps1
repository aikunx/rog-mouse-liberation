# tests\test_device_category_and_battery_isolation.ps1
# 验证无电池设备（键盘、音箱、音频外设）类别精准识别与电量完全隔离（绝不继承鼠标的 78% 假电量）

$ErrorActionPreference = "Stop"

$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) { $asmPath = "FerrisPulse.exe" }
$fullPath = (Resolve-Path $asmPath).Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tRzHelper = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
$tCat = $asm.GetType("RazerBatteryTray.DeviceCategory")

if ($tRzHelper -eq $null -or $tCat -eq $null) {
    Write-Host "FAIL: Required types not found in assembly" -ForegroundColor Red
    exit 1
}

$failed = $false

# 1. 测试音频与键盘类别精准推导（涵盖 Nommo 天狼星、Leviathan 利维坦、Seiren 魔音海妖，以及非 0x0500 范围的音频设备）
$miCategory = $tRzHelper.GetMethod("GetRazerDeviceCategory", [System.Reflection.BindingFlags]"Public,Static")
if ($miCategory -eq $null) {
    Write-Host "FAIL: Method GetRazerDeviceCategory not found" -ForegroundColor Red
    exit 1
}

$testCases = @(
    @{ Pid = 0x00B6; Name = "Razer DeathAdder V3 Pro"; Expected = "Mouse" },
    @{ Pid = 0x00C0; Name = "Razer Viper V3 Pro"; Expected = "Mouse" },
    @{ Pid = 0x026F; Name = "Razer Huntsman V2 Optical Gaming Keyboard"; Expected = "Keyboard" },
    @{ Pid = 0x0245; Name = "Razer BlackWidow V3"; Expected = "Keyboard" },
    @{ Pid = 0x00A5; Name = "Razer Nommo V2 Pro"; Expected = "Headset" },
    @{ Pid = 0x00A6; Name = "Razer Leviathan V2 Pro Soundbar"; Expected = "Headset" },
    @{ Pid = 0x00A7; Name = "Razer Seiren V3 Chroma Audio"; Expected = "Headset" }
)

foreach ($tc in $testCases) {
    $res = $miCategory.Invoke($null, @($tc.Pid, $tc.Name))
    $actual = $res.ToString()
    if ($actual -ne $tc.Expected) {
        Write-Host ("FAIL Category: " + $tc.Name + " (PID 0x" + $tc.Pid.ToString("X4") + "): Expected " + $tc.Expected + ", got " + $actual) -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host ("PASS Category: " + $tc.Name + " -> " + $actual) -ForegroundColor Green
    }
}

# 2. 测试电量隔离：当全局缓存存在 78% 时，对无电池设备（键盘、音箱）绝不能借用 78%，必须为 -1 且非休眠
$miFallback = $tRzHelper.GetMethod("CreateFallbackDeviceInfo", [System.Reflection.BindingFlags]"Public,Static")
if ($miFallback -eq $null) {
    Write-Host "FAIL: Method CreateFallbackDeviceInfo not found on RazerDeviceHelper (RED phase)" -ForegroundColor Red
    $failed = $true
} else {
    $fCachedBatt = $tRzHelper.GetField("CachedBatteryPercent", [System.Reflection.BindingFlags]"Public,Static")
    if ($fCachedBatt -ne $null) {
        $fCachedBatt.SetValue($null, 78)
    }

    # 测试键盘设备兜底
    $devKbd = $miFallback.Invoke($null, @("Razer:1532:026F", 0x026F, "Razer Huntsman V2", $false, $null))
    if ($devKbd.BatteryPercent -ne -1) {
        Write-Host ("FAIL: Keyboard BatteryPercent expected -1, got " + $devKbd.BatteryPercent) -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "PASS: Keyboard BatteryPercent is -1 (isolated from 78%)" -ForegroundColor Green
    }
    if ($devKbd.IsSleeping -ne $false) {
        Write-Host ("FAIL: Keyboard IsSleeping expected false, got true") -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "PASS: Keyboard IsSleeping is false" -ForegroundColor Green
    }

    # 测试音箱外设兜底
    $devAudio = $miFallback.Invoke($null, @("Razer:1532:00A5", 0x00A5, "Razer Nommo V2 Pro", $false, $null))
    if ($devAudio.BatteryPercent -ne -1) {
        Write-Host ("FAIL: Audio Speaker BatteryPercent expected -1, got " + $devAudio.BatteryPercent) -ForegroundColor Red
        $failed = $true
    } else {
        Write-Host "PASS: Audio Speaker BatteryPercent is -1 (isolated from 78%)" -ForegroundColor Green
    }
}

if ($failed) {
    Write-Host "Test failed as expected in RED phase." -ForegroundColor Yellow
    exit 1
}

Write-Host "ALL TESTS PASSED for test_device_category_and_battery_isolation.ps1" -ForegroundColor Green
exit 0

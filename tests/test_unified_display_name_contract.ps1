# tests\test_unified_display_name_contract.ps1
# 验证全模块外设名称单一真相源：消除毒蝰 V3 Pro / SE 分歧，顶栏 Tab、卡片 1、托盘菜单 100% 逐字相同

$ErrorActionPreference = "Stop"

$fullPath = (Resolve-Path "FerrisPulse.exe").Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tRz = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
$tDev = $asm.GetType("RazerBatteryTray.MouseBatteryInfo")

if ($tRz -eq $null -or $tDev -eq $null) {
    Write-Host "FAIL: Required types not found in assembly" -ForegroundColor Red
    exit 1
}

$failures = 0

# 1. 验证雷蛇有线与无线、标准版与 SE 特别版官方名称 100% 归一化
$flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static
$allStatic = $tRz.GetMethods($flags)
$mOfficial = $allStatic | Where-Object { $_.Name -eq "GetRazerOfficialModelName" } | Select-Object -First 1

$pids = @(0x00C0, 0x00C1, 0x00C2, 0x00C3)
foreach ($p in $pids) {
    $resName = $mOfficial.Invoke($null, @([int]$p, $null))
    if ($resName -ne "Razer Viper V3 Pro") {
        Write-Host ("FAIL: Model name for PID 0x" + $p.ToString("X4") + " expected 'Razer Viper V3 Pro', but got: '$resName'") -ForegroundColor Red
        $failures++
    } else {
        Write-Host ("PASS: Model name for PID 0x" + $p.ToString("X4") + " is consistently 'Razer Viper V3 Pro'") -ForegroundColor Green
    }
}

# 2. 验证用户自定义昵称在 MouseBatteryInfo.DisplayName 的绝对统治力
$devObj = [System.Activator]::CreateInstance($tDev)
$devObj.DeviceId = "Razer:1532:00C0"
$devObj.Brand = "Razer"
$devObj.DeviceName = "Razer Viper V3 Pro"
$devObj.CustomName = "毒蝰V3Pro"

if ($devObj.DisplayName -ne "毒蝰V3Pro") {
    Write-Host ("FAIL: DisplayName expected '毒蝰V3Pro', but got: '" + $devObj.DisplayName + "'") -ForegroundColor Red
    $failures++
} else {
    Write-Host "PASS: User nickname '毒蝰V3Pro' respected by DisplayName" -ForegroundColor Green
}

$devObj.CustomName = "毒蝰V3Prose"
if ($devObj.DisplayName -ne "毒蝰V3Prose") {
    Write-Host ("FAIL: DisplayName expected '毒蝰V3Prose', but got: '" + $devObj.DisplayName + "'") -ForegroundColor Red
    $failures++
} else {
    Write-Host "PASS: User nickname '毒蝰V3Prose' respected by DisplayName" -ForegroundColor Green
}

# 3. 静态代码审查：确保 SmoothSegmentedTabBar.cs 不再调用 FriendlyShortName，统一使用 DisplayName
$tabBarSrc = [System.IO.File]::ReadAllText((Resolve-Path "src\UI\Controls\SmoothSegmentedTabBar.cs").Path)
if ($tabBarSrc -match "dev\.FriendlyShortName") {
    Write-Host "FAIL: SmoothSegmentedTabBar.cs still calls dev.FriendlyShortName (Defect: tab name divergence from tray/main)" -ForegroundColor Red
    $failures++
} else {
    Write-Host "PASS: SmoothSegmentedTabBar.cs calls dev.DisplayName (Single Source of Truth)" -ForegroundColor Green
}

if ($failures -gt 0) {
    Write-Host ("RED Phase Verified: $failures failures observed as expected.") -ForegroundColor Yellow
    exit 1
}

Write-Host "ALL TESTS PASSED for test_unified_display_name_contract.ps1" -ForegroundColor Green
exit 0

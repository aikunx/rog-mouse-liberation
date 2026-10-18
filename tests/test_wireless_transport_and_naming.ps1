# tests\test_wireless_transport_and_naming.ps1
# 验证物理状态推导（电量>0 且非充电即为 2.4G 无线）与雷蛇特别版（如 0x00C3 毒蝰 V3 Pro SE）归一化

$ErrorActionPreference = "Stop"

$fullPath = (Resolve-Path "FerrisPulse.exe").Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tRz = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
if ($tRz -eq $null) {
    Write-Host "FAIL: RazerDeviceHelper type not found" -ForegroundColor Red
    exit 1
}

$failures = 0

# 1. 验证 DetermineIsDongle 物理推导方法
$mDongle = $tRz.GetMethod("DetermineIsDongle")
if ($mDongle -eq $null) {
    Write-Host "FAIL: DetermineIsDongle method not found" -ForegroundColor Red
    $failures++
} else {
    # Case 1: 毒蝰 V3 Pro SE（特殊 PID 0x00C3），无 Dongle 字符串，电量 85% 非充电 -> 物理铁律必为无线
    $r1 = $mDongle.Invoke($null, @([int]0x00C3, "Razer Viper V3 Pro", 85, $false))
    if ($r1 -eq $true) {
        Write-Host "PASS: Viper V3 Pro SE correctly determined as wireless via physical battery state" -ForegroundColor Green
    } else {
        Write-Host "FAIL: Viper V3 Pro SE with battery > 0 and !charging must be Dongle/Wireless" -ForegroundColor Red
        $failures++
    }

    # Case 2: 纯未知 PID 0x00FF，无任何字典收录，电量 60% 非充电 -> 物理铁律自适应为无线
    $r2 = $mDongle.Invoke($null, @([int]0x00FF, "Razer Wireless Mouse", 60, $false))
    if ($r2 -eq $true) {
        Write-Host "PASS: Unknown future PID dynamically recognized as 2.4G wireless" -ForegroundColor Green
    } else {
        Write-Host "FAIL: Unknown PID with battery > 0 and !charging must be Dongle/Wireless" -ForegroundColor Red
        $failures++
    }

    # Case 3: 有线连接，供电中/充电中 -> 判定为有线
    $r3 = $mDongle.Invoke($null, @([int]0x00C0, "Razer Viper V3 Pro", 90, $true))
    if ($r3 -eq $false) {
        Write-Host "PASS: Wired charging mouse correctly determined as wired" -ForegroundColor Green
    } else {
        Write-Host "FAIL: Wired charging mouse must not be Dongle" -ForegroundColor Red
        $failures++
    }
}

# 2. 验证 NormalizeRazerPid 映射收敛
$mNorm = $tRz.GetMethod("NormalizeRazerPid")
if ($mNorm -eq $null) {
    Write-Host "FAIL: NormalizeRazerPid method not found" -ForegroundColor Red
    $failures++
} else {
    $normC3 = $mNorm.Invoke($null, @([int]0x00C3))
    if ($normC3 -eq 0x00C0) {
        Write-Host "PASS: PID 0x00C3 successfully normalized to 0x00C0" -ForegroundColor Green
    } else {
        Write-Host ("FAIL: PID 0x00C3 expected normalized to 0x00C0, but got: 0x" + $normC3.ToString("X4")) -ForegroundColor Red
        $failures++
    }

    $normC2 = $mNorm.Invoke($null, @([int]0x00C2))
    if ($normC2 -eq 0x00C0) {
        Write-Host "PASS: PID 0x00C2 successfully normalized to 0x00C0" -ForegroundColor Green
    } else {
        Write-Host ("FAIL: PID 0x00C2 expected normalized to 0x00C0, but got: 0x" + $normC2.ToString("X4")) -ForegroundColor Red
        $failures++
    }
}

# 3. 验证 GetRazerOfficialModelName 模型名称归一化
$flags = [System.Reflection.BindingFlags]::Public -bor [System.Reflection.BindingFlags]::Static
$allStatic = $tRz.GetMethods($flags)
$mOfficial = $allStatic | Where-Object { $_.Name -eq "GetRazerOfficialModelName" } | Select-Object -First 1
if ($mOfficial -eq $null) {
    Write-Host "FAIL: GetRazerOfficialModelName method not found" -ForegroundColor Red
    $failures++
} else {
    $nameC3 = $mOfficial.Invoke($null, @([int]0x00C3, $null))
    if ($nameC3 -eq "Razer Viper V3 Pro") {
        Write-Host "PASS: 0x00C3 successfully resolves to 'Razer Viper V3 Pro'" -ForegroundColor Green
    } else {
        Write-Host ("FAIL: Official name for 0x00C3 expected 'Razer Viper V3 Pro', but got: '$nameC3'") -ForegroundColor Red
        $failures++
    }
}

if ($failures -gt 0) {
    Write-Host ("Test Failed: $failures failures observed.") -ForegroundColor Red
    exit 1
}

Write-Host "ALL TESTS PASSED for test_wireless_transport_and_naming.ps1" -ForegroundColor Green
exit 0

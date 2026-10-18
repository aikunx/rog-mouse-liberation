# tests\test_category_user_override.ps1
# 验证单一真相源：用户在注册表/向导页设定的 Category 拥有最高绝对优先级，调度器严禁改写为鼠标

$ErrorActionPreference = "Stop"

$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) { $asmPath = "FerrisPulse.exe" }
$fullPath = (Resolve-Path $asmPath).Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tMgr = $asm.GetType("RazerBatteryTray.DeviceManager")
$tCat = $asm.GetType("RazerBatteryTray.DeviceCategory")

if ($tMgr -eq $null -or $tCat -eq $null) {
    Write-Host "FAIL: DeviceManager or DeviceCategory type not found" -ForegroundColor Red
    exit 1
}

$testDevId = "Mock:9999:FEFE"
$regCustomPath = "HKCU:\Software\RazerBatteryTray\CustomDevices\$testDevId"

try {
    # 1. 注入用户自定义配置：将设备设定为 Headset (耳机/音箱)
    if (-not (Test-Path $regCustomPath)) {
        New-Item -Path $regCustomPath -Force | Out-Null
    }

    Set-ItemProperty -Path $regCustomPath -Name "DeviceId" -Value $testDevId
    Set-ItemProperty -Path $regCustomPath -Name "HardwareFingerprint" -Value $testDevId
    Set-ItemProperty -Path $regCustomPath -Name "OriginalName" -Value "Mock Device"
    Set-ItemProperty -Path $regCustomPath -Name "CustomName" -Value "测试模拟桌面音箱"
    Set-ItemProperty -Path $regCustomPath -Name "Category" -Value 1 # 1 = Headset
    Set-ItemProperty -Path $regCustomPath -Name "Transport" -Value "2.4G 无线"
    Set-ItemProperty -Path $regCustomPath -Name "CachedBattery" -Value 66

    Write-Host "Injected mock override config: Category = Headset for $testDevId"

    # 2. 调用 DeviceManager.QueryAllDevices()
    $miQuery = $tMgr.GetMethod("QueryAllDevices", [System.Reflection.BindingFlags]"Public,Static")
    if ($miQuery -eq $null) {
        Write-Host "FAIL: Method QueryAllDevices not found" -ForegroundColor Red
        exit 1
    }

    $devices = $miQuery.Invoke($null, @())
    $target = $null
    foreach ($d in $devices) {
        if ($d.DeviceId -eq $testDevId) {
            $target = $d
            break
        }
    }

    if ($target -eq $null) {
        Write-Host "FAIL: Device $testDevId not found in QueryAllDevices() result" -ForegroundColor Red
        exit 1
    }

    Write-Host ("Queried Device Category: " + $target.Category + ", DeviceName: " + $target.DeviceName + ", DisplayName: " + $target.DisplayName)

    # 3. 断言 Category 必须是 Headset，绝不能被驱动层覆写为 Mouse
    if ($target.Category.ToString() -ne "Headset") {
        Write-Host ("FAIL: User configured Category as Headset, but got: " + $target.Category.ToString()) -ForegroundColor Red
        exit 1
    } else {
        Write-Host "PASS: User Category successfully preserved as Headset" -ForegroundColor Green
    }

    Write-Host "ALL TESTS PASSED for test_category_user_override.ps1" -ForegroundColor Green
}
finally {
    # 无论测试成功还是失败，严格清理测试生成的临时注册表项，绝对不污染用户真实硬件设备！
    if (Test-Path $regCustomPath) {
        Remove-Item -Path $regCustomPath -Force -Recurse -ErrorAction SilentlyContinue
    }
}
exit 0


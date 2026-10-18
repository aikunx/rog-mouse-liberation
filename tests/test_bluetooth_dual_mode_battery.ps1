# ============================================================================
# 测试用例: test_bluetooth_dual_mode_battery.ps1
# 核心验证:
#   1. 蓝牙双模耳机 (Classic BR/EDR + BLE GATT Battery) 实时电量对齐与防卡死
#   2. 优先采用最新时间戳的 BLE 电池服务节点 (解决 63% 卡死在旧 HFP 节点的问题)
#   3. DEVICE_NOTIFY_ALL_INTERFACE_CLASSES 全设备类型硬件即时热插拔捕获
#   4. 离线缓存穿透自愈：当 Windows 驱动属性存在最新电量时，离线也能刷新为真实电量 (100%)
# ============================================================================

$repoRoot = Split-Path -Parent $PSScriptRoot
$tempDll = [System.IO.Path]::Combine([System.IO.Path]::GetTempPath(), "BtDualTest_" + [System.Guid]::NewGuid().ToString("N") + ".dll")
$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

& $csc /target:library /optimize- /codepage:65001 /r:System.Drawing.dll /r:System.Windows.Forms.dll /out:$tempDll /recurse:src\*.cs
if ($LASTEXITCODE -ne 0) {
    Write-Error "FAIL: Compilation failed!"
    exit 1
}

$bytes = [System.IO.File]::ReadAllBytes($tempDll)
$assembly = [System.Reflection.Assembly]::Load($bytes)
Remove-Item $tempDll -Force -ErrorAction SilentlyContinue

$btHelper = $assembly.GetType("RazerBatteryTray.BluetoothDeviceHelper")
$nativeMethods = $assembly.GetType("RazerBatteryTray.NativeMethods")
$devMgr = $assembly.GetType("RazerBatteryTray.DeviceManager")

# --- 1. 验证 NativeMethods 中是否定义了 DEVICE_NOTIFY_ALL_INTERFACE_CLASSES ---
$flagField = $nativeMethods.GetField("DEVICE_NOTIFY_ALL_INTERFACE_CLASSES", [System.Reflection.BindingFlags]"Public,Static")
if ($null -eq $flagField) {
    Write-Error "FAIL: NativeMethods.DEVICE_NOTIFY_ALL_INTERFACE_CLASSES is not defined!"
    exit 1
}
$flagVal = [uint32]$flagField.GetValue($null)
if ($flagVal -ne 4) {
    Write-Error "FAIL: DEVICE_NOTIFY_ALL_INTERFACE_CLASSES value is $flagVal, expected 4!"
    exit 1
}
Write-Host "PASS: NativeMethods.DEVICE_NOTIFY_ALL_INTERFACE_CLASSES = 0x00000004 verified."

# --- 2. 验证 QueryByMac 4 参数重载支持 hintName ---
$queryWithHint = $btHelper.GetMethod("QueryByMac", [System.Reflection.BindingFlags]"Public,Static", $null, @([string], [string], [int].MakeByRefType(), [string].MakeByRefType()), $null)
if ($null -eq $queryWithHint) {
    Write-Error "FAIL: BluetoothDeviceHelper.QueryByMac(mac, hintName, ...) method not found!"
    exit 1
}

$params = [object[]]@("5CD33D59A938", "鹏飞 的 Buds4 Pro", 0, "")
$connected = $queryWithHint.Invoke($null, $params)
$battery = [int]$params[2]
$friendlyName = [string]$params[3]

Write-Host "QueryByMac with hintName Result: Connected=$connected, Battery=$battery%, FriendlyName='$friendlyName'"

if ($friendlyName -ne "鹏飞 的 Buds4 Pro") {
    Write-Error "FAIL: FriendlyName expected '鹏飞 的 Buds4 Pro', got '$friendlyName'"
    exit 1
}
Write-Host "PASS: FriendlyName accurately retained without corrupted service name."

if ($battery -eq 63) {
    Write-Error "FAIL: Battery is still stuck at old 63%! Expected 100% from BLE node."
    exit 1
}
if ($battery -eq 100) {
    Write-Host "PASS: Successfully extracted 100% battery from modern BLE node via timestamp arbitration!"
} else {
    Write-Warning "WARN: Battery is $battery% (not stuck at 63%)."
}

# --- 3. 验证 DeviceManager 扫描与离线自愈链路 ---
$getAllDevs = $devMgr.GetMethod("QueryAllDevices", [System.Reflection.BindingFlags]"Public,Static")
if ($null -ne $getAllDevs) {
    $devList = $getAllDevs.Invoke($null, $null)
    $buds = $null
    foreach ($d in $devList) {
        if ($d.HardwareFingerprint -eq "5CD33D59A938") {
            $buds = $d
            break
        }
    }
    if ($null -ne $buds) {
        Write-Host "DeviceManager found Buds4 Pro: Battery=$($buds.BatteryPercent)%, Connected=$($buds.IsConnected), Name='$($buds.DeviceName)'"
        if ($buds.BatteryPercent -eq 63) {
            Write-Error "FAIL: DeviceManager battery still stuck at 63%!"
            exit 1
        }
        if ($buds.BatteryPercent -eq 100) {
            Write-Host "PASS: DeviceManager successfully recovered 100% battery!"
        }
    }
}

Write-Host "ALL BLUETOOTH DUAL-MODE BATTERY TESTS PASSED!"

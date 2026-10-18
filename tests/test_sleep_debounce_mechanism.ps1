# tests\test_sleep_debounce_mechanism.ps1
# 验证托盘开窗防抖与待机误报消除：单次/偶然查询超时绝不误判黄色待机，需连续 3 轮失败且静止超 45 秒

$ErrorActionPreference = "Stop"

$fullPath = (Resolve-Path "FerrisPulse.exe").Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tRz = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
$tState = $asm.GetType("RazerBatteryTray.RazerDeviceHardwareState")

if ($tRz -eq $null -or $tState -eq $null) {
    Write-Host "FAIL: RazerDeviceHelper or RazerDeviceHardwareState type not found" -ForegroundColor Red
    exit 1
}

$failures = 0

# 1. 验证 RazerDeviceHardwareState 具备防抖计数器与活跃时间戳
$propFailures = $tState.GetField("ConsecutiveFailures")
$propLastComm = $tState.GetField("LastSuccessfulCommunication")

if ($propFailures -eq $null) {
    Write-Host "FAIL: ConsecutiveFailures field not found in RazerDeviceHardwareState (RED phase)" -ForegroundColor Red
    $failures++
} else {
    Write-Host "PASS: ConsecutiveFailures field exists in RazerDeviceHardwareState" -ForegroundColor Green
}

if ($propLastComm -eq $null) {
    Write-Host "FAIL: LastSuccessfulCommunication field not found in RazerDeviceHardwareState (RED phase)" -ForegroundColor Red
    $failures++
} else {
    Write-Host "PASS: LastSuccessfulCommunication field exists in RazerDeviceHardwareState" -ForegroundColor Green
}

# 2. 验证 CreateFallbackDeviceInfo 防抖决策
$miFallback = $tRz.GetMethod("CreateFallbackDeviceInfo")
if ($miFallback -eq $null) {
    Write-Host "FAIL: CreateFallbackDeviceInfo method not found" -ForegroundColor Red
    $failures++
} else {
    $normId = "Razer:1532:00C0"
    $curPid = 0x00C1 # Viper V3 Pro Dongle
    $modelName = "Razer Viper V3 Pro"
    $isDongle = $true

    # Scenario A: 单次偶发超时（开窗或轮询瞬间，连续失败 1 次，5 秒前才刚刚成功通信）
    $stA = [System.Activator]::CreateInstance($tState)
    $stA.DeviceId = $normId
    $stA.BatteryPercent = 88
    $stA.ExactBatteryPercent = 88.2
    if ($propFailures -ne $null) { $propFailures.SetValue($stA, 1) }
    if ($propLastComm -ne $null) { $propLastComm.SetValue($stA, [DateTime]::Now.AddSeconds(-5)) }

    $devA = $miFallback.Invoke($null, @($normId, $curPid, $modelName, $isDongle, $stA))
    if ($devA.IsSleeping -eq $true) {
        Write-Host "FAIL: Single transient failure marked device as IsSleeping (Defect: yellow pill flash on window open)" -ForegroundColor Red
        $failures++
    } else {
        Write-Host "PASS: Single transient failure correctly debounced, device remains active" -ForegroundColor Green
    }

    # Scenario B: 连续 2 轮超时，20 秒前才通信（依然处于防抖窗口内）
    $stB = [System.Activator]::CreateInstance($tState)
    $stB.DeviceId = $normId
    $stB.BatteryPercent = 88
    if ($propFailures -ne $null) { $propFailures.SetValue($stB, 2) }
    if ($propLastComm -ne $null) { $propLastComm.SetValue($stB, [DateTime]::Now.AddSeconds(-20)) }

    $devB = $miFallback.Invoke($null, @($normId, $curPid, $modelName, $isDongle, $stB))
    if ($devB.IsSleeping -eq $true) {
        Write-Host "FAIL: 2 consecutive failures within 20s must remain active" -ForegroundColor Red
        $failures++
    } else {
        Write-Host "PASS: 2 consecutive failures successfully debounced" -ForegroundColor Green
    }

    # Scenario C: 连续 3 轮失败且静止超 45 秒 -> 准许进入真正待机状态
    $stC = [System.Activator]::CreateInstance($tState)
    $stC.DeviceId = $normId
    $stC.BatteryPercent = 88
    if ($propFailures -ne $null) { $propFailures.SetValue($stC, 3) }
    if ($propLastComm -ne $null) { $propLastComm.SetValue($stC, [DateTime]::Now.AddSeconds(-50)) }

    $devC = $miFallback.Invoke($null, @($normId, $curPid, $modelName, $isDongle, $stC))
    if ($devC.IsSleeping -ne $true) {
        Write-Host "FAIL: 3 consecutive failures + >45s idle should enter IsSleeping" -ForegroundColor Red
        $failures++
    } else {
        Write-Host "PASS: Real idle state (>45s + 3 failures) correctly enters IsSleeping" -ForegroundColor Green
    }
}

if ($failures -gt 0) {
    Write-Host ("RED Phase Verified: $failures failures observed as expected.") -ForegroundColor Yellow
    exit 1
}

Write-Host "ALL TESTS PASSED for test_sleep_debounce_mechanism.ps1" -ForegroundColor Green
exit 0

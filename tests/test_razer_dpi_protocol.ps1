# tests\test_razer_dpi_protocol.ps1
$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) {
    $asmPath = "FerrisPulse.exe"
}
if (-not (Test-Path $asmPath)) {
    Write-Host "RED: FerrisPulse.exe not found for testing!" -ForegroundColor Red
    exit 1
}
$fullPath = (Resolve-Path $asmPath).Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)
$tHelper = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
$tState = $asm.GetType("RazerBatteryTray.RazerDeviceHardwareState")

# Test 1: Check ActiveTransactionId field on RazerDeviceHardwareState
$propTid = $tState.GetProperty("ActiveTransactionId")
$fieldTid = $tState.GetField("ActiveTransactionId")
if ($propTid -eq $null -and $fieldTid -eq $null) {
    Write-Host "RED: ActiveTransactionId not found on RazerDeviceHardwareState!" -ForegroundColor Red
    exit 1
}

# Test 2: Check GetAdaptiveTransactionIds method on RazerDeviceHelper
$mAdaptive = $tHelper.GetMethod("GetAdaptiveTransactionIds", [System.Reflection.BindingFlags]'Public,NonPublic,Static')
if ($mAdaptive -eq $null) {
    Write-Host "RED: GetAdaptiveTransactionIds method not found on RazerDeviceHelper!" -ForegroundColor Red
    exit 1
}

# Verify priority sequences:
$res3F = [byte[]]$mAdaptive.Invoke($null, @([byte]0x3F))
if ($res3F[0] -ne 0x3F) {
    Write-Host "FAIL: Preferred 0x3F did not place 0x3F at index 0!" -ForegroundColor Red
    exit 1
}

$resFF = [byte[]]$mAdaptive.Invoke($null, @([byte]0xFF))
if ($resFF[0] -ne 0xFF) {
    Write-Host "FAIL: Preferred 0xFF did not place 0xFF at index 0!" -ForegroundColor Red
    exit 1
}

$res1F = [byte[]]$mAdaptive.Invoke($null, @([byte]0x1F))
if ($res1F[0] -ne 0x1F) {
    Write-Host "FAIL: Preferred 0x1F did not place 0x1F at index 0!" -ForegroundColor Red
    exit 1
}

# Test 3: Check packet generation for 0x1F, 0x3F, 0xFF
$mCreate = $tHelper.GetMethod("CreateRazerReport", [System.Reflection.BindingFlags]'NonPublic,Static')
$argsDpi = [byte[]]@(0x01, 0x06, 0x40, 0x06, 0x40, 0x00, 0x00) # 1600 DPI
foreach ($tid in @([byte]0x1F, [byte]0x3F, [byte]0xFF)) {
    $report = $mCreate.Invoke($null, @($tid, [byte]0x04, [byte]0x05, [byte]0x07, 90, $false, $argsDpi))
    if ($report[1] -ne $tid) {
        Write-Host "FAIL: Report Transaction ID mismatch for $tid! Got: $($report[1])" -ForegroundColor Red
        exit 1
    }
    if ($report[6] -ne 0x04 -or $report[7] -ne 0x05) {
        Write-Host "FAIL: Command Class/ID mismatch! Class: $($report[6]), ID: $($report[7])" -ForegroundColor Red
        exit 1
    }
}

Write-Host "ALL RAZER DPI PROTOCOL TESTS PASSED!" -ForegroundColor Green

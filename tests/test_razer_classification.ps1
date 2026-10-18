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
$t = $asm.GetType("RazerBatteryTray.RazerDeviceHelper")
$m = $t.GetMethod("GetRazerDeviceCategory", [System.Reflection.BindingFlags]'Public,Static')

if ($m -eq $null) {
    Write-Host "RED: GetRazerDeviceCategory method does not exist on RazerDeviceHelper!" -ForegroundColor Red
    exit 1
}

$cases = @(
    @{ Pid = 0x0216; Name = "Razer BlackWidow X Chroma"; Expected = "Keyboard" },
    @{ Pid = 0x0217; Name = "Razer BlackWidow X Ultimate"; Expected = "Keyboard" },
    @{ Pid = 0x021A; Name = "Razer BlackWidow X Chroma TE"; Expected = "Keyboard" },
    @{ Pid = 0x0203; Name = "Razer BlackWidow Chroma"; Expected = "Keyboard" },
    @{ Pid = 0x0226; Name = "Razer Huntsman Elite"; Expected = "Keyboard" },
    @{ Pid = 0x021E; Name = "Razer Ornata Chroma"; Expected = "Keyboard" },
    @{ Pid = 0x022A; Name = "Razer Cynosa Chroma"; Expected = "Keyboard" },
    @{ Pid = 0x00B6; Name = "Razer DeathAdder V3 Pro (Wired)"; Expected = "Mouse" },
    @{ Pid = 0x00B7; Name = "Razer DeathAdder V3 Pro (Wireless)"; Expected = "Mouse" },
    @{ Pid = 0x008A; Name = "Razer Viper Mini"; Expected = "Mouse" },
    @{ Pid = 0x0078; Name = "Razer Viper Ultimate"; Expected = "Mouse" }
)

$allPassed = $true
foreach ($c in $cases) {
    $res = $m.Invoke($null, @($c.Pid, $c.Name)).ToString()
    if ($res -ne $c.Expected) {
        Write-Host "FAIL: PID=$($c.Pid) Name=$($c.Name) Expected=$($c.Expected) Got=$res" -ForegroundColor Red
        $allPassed = $false
    } else {
        Write-Host "PASS: PID=$($c.Pid) Name=$($c.Name) -> $res" -ForegroundColor Green
    }
}
if (-not $allPassed) { exit 1 } else { Write-Host "ALL CLASSIFICATION TESTS PASSED!" -ForegroundColor Cyan }

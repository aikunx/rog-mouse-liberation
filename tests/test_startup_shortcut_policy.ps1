# tests\test_startup_shortcut_policy.ps1
# Clean portable desktop shortcut test
$ErrorActionPreference = "Stop"

$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) { $asmPath = "FerrisPulse.exe" }
$fullPath = (Resolve-Path $asmPath).Path
$asm = [System.Reflection.Assembly]::LoadFile($fullPath)

$tStartup = $asm.GetType("RazerBatteryTray.StartupManager")
$tConfig = $asm.GetType("RazerBatteryTray.ConfigManager")

if ($tStartup -eq $null) {
    Write-Host "FAIL: Type RazerBatteryTray.StartupManager not found" -ForegroundColor Red
    exit 1
}

# 1. Contract verification
$mCreate = $tStartup.GetMethod("CreateDesktopShortcut", [System.Reflection.BindingFlags]"Public,Static")
if ($mCreate -eq $null) {
    Write-Host "FAIL: Method StartupManager.CreateDesktopShortcut not found!" -ForegroundColor Red
    exit 1
}

$mFirstRun = $tStartup.GetMethod("EnsureDesktopShortcutFirstRun", [System.Reflection.BindingFlags]"Public,Static")
if ($mFirstRun -eq $null) {
    Write-Host "FAIL: Method StartupManager.EnsureDesktopShortcutFirstRun not found!" -ForegroundColor Red
    exit 1
}

$mAumid = $tStartup.GetMethod("EnsureAppShortcutWithAumid", [System.Reflection.BindingFlags]"Public,Static")
if ($mAumid -eq $null) {
    Write-Host "FAIL: Method StartupManager.EnsureAppShortcutWithAumid not found!" -ForegroundColor Red
    exit 1
}

Write-Host "PASS: Public APIs contracts exist." -ForegroundColor Green

$desktop = [Environment]::GetFolderPath([Environment+SpecialFolder]::Desktop)
$desktopLnk = Join-Path $desktop "FerrisPulse · 灵脉.lnk"
$backupLnk = Join-Path ([System.IO.Path]::GetTempPath()) ("FerrisPulse_Desktop_Backup_" + [System.Guid]::NewGuid().ToString("N") + ".lnk")
$hadExistingShortcut = Test-Path $desktopLnk

if ($hadExistingShortcut) {
    Copy-Item -LiteralPath $desktopLnk -Destination $backupLnk -Force
    Remove-Item -Force $desktopLnk
}

try {
    # Reset registry flag to 0 to simulate clean / first run
    $regKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey("Software\FerrisPulse")
    $regKey.SetValue("DesktopShortcutInitialized", 0, [Microsoft.Win32.RegistryValueKind]::DWord)

    # Scenario A: First manual run (isAutoStart = false) MUST NOT create desktop shortcut
    $mFirstRun.Invoke($null, @($false))
    if (Test-Path $desktopLnk) {
        Write-Host "FAIL: EnsureDesktopShortcutFirstRun unexpectedly created a shortcut on Desktop during manual run!" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS: Scenario A - Manual run strictly keeps Desktop clean." -ForegroundColor Green

    # Scenario B: Autostart run (isAutoStart = true) MUST NOT create desktop shortcut
    $mFirstRun.Invoke($null, @($true))
    if (Test-Path $desktopLnk) {
        Write-Host "FAIL: EnsureDesktopShortcutFirstRun unexpectedly created a shortcut on Desktop during autostart!" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS: Scenario B - AutoStart run strictly keeps Desktop clean." -ForegroundColor Green

    # Scenario C: EnsureAppShortcutWithAumid must not touch Desktop
    $mAumid.Invoke($null, $null)
    if (Test-Path $desktopLnk) {
        Write-Host "FAIL: EnsureAppShortcutWithAumid leaked a shortcut onto Desktop!" -ForegroundColor Red
        exit 1
    }
    $startMenu = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) "Microsoft\Windows\Start Menu\Programs"
    $startLnk = Join-Path $startMenu "FerrisPulse · 灵脉.lnk"
    if (-not (Test-Path $startLnk)) {
        Write-Host "FAIL: Start Menu AUMID shortcut was not created!" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS: Scenario C - AUMID shortcut correctly placed only in Start Menu." -ForegroundColor Green

    # Scenario D: Manual CreateDesktopShortcut from tray MUST succeed
    $retD = [bool]$mCreate.Invoke($null, @($false))
    if (-not $retD -or -not (Test-Path $desktopLnk)) {
        Write-Host "FAIL: Manual CreateDesktopShortcut failed to create shortcut on Desktop!" -ForegroundColor Red
        exit 1
    }

    $wsh = New-Object -ComObject WScript.Shell
    $sc = $wsh.CreateShortcut($desktopLnk)
    if ($sc.TargetPath -like "*powershell*" -or $sc.TargetPath -like "*cmd.exe*") {
        Write-Host "FAIL: Desktop shortcut TargetPath was corrupted to: $($sc.TargetPath)!" -ForegroundColor Red
        exit 1
    }
    if (-not ($sc.TargetPath -like "*FerrisPulse.exe*")) {
        Write-Host "FAIL: Desktop shortcut TargetPath does not point to FerrisPulse.exe: $($sc.TargetPath)!" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS: Scenario D - Manual invocation explicitly and accurately creates desktop shortcut: $($sc.TargetPath)" -ForegroundColor Green

    Write-Host "ALL TESTS PASSED for test_startup_shortcut_policy.ps1" -ForegroundColor Green
}
finally {
    # 彻底恢复用户原有的桌面快捷方式，严禁破坏用户桌面状态
    if ($hadExistingShortcut -and (Test-Path $backupLnk)) {
        Move-Item -LiteralPath $backupLnk -Destination $desktopLnk -Force
        Write-Host "PASS: Restored user's original desktop shortcut after test." -ForegroundColor DarkGray
    } elseif (Test-Path $desktopLnk) {
        Remove-Item -Force $desktopLnk
    }
}
exit 0

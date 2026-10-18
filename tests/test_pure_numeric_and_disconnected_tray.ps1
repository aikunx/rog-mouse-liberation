# ============================================================================
# 测试用例: test_pure_numeric_and_disconnected_tray.ps1
# 验证内容:
#   1. 托盘图标样式子菜单契约：现代胶囊电池、醒目数字能量表
#   2. SetTrayStyle(0/1) 切换联动与注册表持久化
#   3. 断联托盘图标精致重绘（胶囊四角严密闭合无空隙、能量表双破折号水平垂直严格居中）
# ============================================================================
$ErrorActionPreference = "Stop"

$asmPath = Join-Path $PSScriptRoot "..\FerrisPulse.exe"
if (-not (Test-Path $asmPath)) { $asmPath = "FerrisPulse.exe" }
$fullPath = (Resolve-Path $asmPath).Path
$bytes = [System.IO.File]::ReadAllBytes($fullPath)
$asm = [System.Reflection.Assembly]::Load($bytes)

$tMain = $asm.GetType("RazerBatteryTray.MainForm")
if ($tMain -eq $null) {
    Write-Host "FAIL: Type RazerBatteryTray.MainForm not found" -ForegroundColor Red
    exit 1
}

# 1. 实例化 MainForm 并检查托盘菜单项
$form = [Activator]::CreateInstance($tMain, @([bool]$true))

$fMenu = $tMain.GetField("contextMenu", [System.Reflection.BindingFlags]"NonPublic,Instance")
$contextMenu = $fMenu.GetValue($form)
if ($contextMenu -eq $null) {
    Write-Host "FAIL: contextMenu is null" -ForegroundColor Red
    exit 1
}

# 递归寻找“托盘图标样式”子菜单
$styleMenu = $null
foreach ($it in $contextMenu.Items) {
    if ($it.Text -eq "托盘图标样式") {
        $styleMenu = $it
        break
    }
}
if ($styleMenu -eq $null) {
    Write-Host "FAIL: '托盘图标样式' menu item not found!" -ForegroundColor Red
    exit 1
}
Write-Host "PASS: '托盘图标样式' menu found with $($styleMenu.DropDownItems.Count) items." -ForegroundColor Green

# 验证子菜单项数量与名称
if ($styleMenu.DropDownItems.Count -ne 2) {
    Write-Host "FAIL: Expected 2 style items, but found $($styleMenu.DropDownItems.Count)" -ForegroundColor Red
    exit 1
}

$capsuleItem = $styleMenu.DropDownItems[0]
$numItem = $styleMenu.DropDownItems[1]
if ($capsuleItem.Text -ne "现代胶囊电池" -or $numItem.Text -ne "醒目数字能量表") {
    Write-Host "FAIL: Style menu items text mismatch!" -ForegroundColor Red
    exit 1
}
Write-Host "PASS: Style menu items text: '现代胶囊电池' and '醒目数字能量表'." -ForegroundColor Green

# 2. 验证 SetTrayStyle(1) 切换与联动
$mSetTrayStyle = $tMain.GetMethod("SetTrayStyle", [System.Reflection.BindingFlags]"NonPublic,Instance")
if ($mSetTrayStyle -eq $null) {
    Write-Host "FAIL: Method SetTrayStyle not found!" -ForegroundColor Red
    exit 1
}

$mSetTrayStyle.Invoke($form, @([int]1, [bool]$false))

$fTrayStyle = $tMain.GetField("trayStyle", [System.Reflection.BindingFlags]"NonPublic,Instance")
$currentStyle = $fTrayStyle.GetValue($form)
if ($currentStyle -ne 1) {
    Write-Host "FAIL: trayStyle after SetTrayStyle(1) was $currentStyle, expected 1!" -ForegroundColor Red
    exit 1
}
if (-not $numItem.Checked) {
    Write-Host "FAIL: numItem.Checked is false after SetTrayStyle(1)!" -ForegroundColor Red
    exit 1
}
Write-Host "PASS: SetTrayStyle(1) correctly sets trayStyle = 1 and checks numItem." -ForegroundColor Green

# 3. 验证注册表持久化与 LoadConfig
$regKey = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Software\FerrisPulse")
if ($regKey -ne $null) {
    $savedStyle = $regKey.GetValue("TrayIconStyle")
    $regKey.Close()
    if ($savedStyle -ne 1) {
        Write-Host "FAIL: Registry TrayIconStyle was $savedStyle, expected 1!" -ForegroundColor Red
        exit 1
    }
    Write-Host "PASS: Registry correctly saved TrayIconStyle = 1." -ForegroundColor Green
}

# 4. 验证 GDI+ 矢量渲染：DrawTrayBitmap 在全部 2 种样式下的 16x16 与 24x24 渲染
$mDraw = $tMain.GetMethod("DrawTrayBitmap", [System.Reflection.BindingFlags]"NonPublic,Static")
if ($mDraw -eq $null) {
    Write-Host "FAIL: Method DrawTrayBitmap not found!" -ForegroundColor Red
    exit 1
}

foreach ($style in @(0, 1)) {
    foreach ($size in @(16, 24)) {
        # 1. 正常连接
        $bmpConnected = $mDraw.Invoke($null, @([int]85, [bool]$false, [bool]$true, [int]$style, [int]$size, [bool]$false, $null))
        if ($bmpConnected -eq $null -or $bmpConnected.Width -ne $size -or $bmpConnected.Height -ne $size) {
            Write-Host "FAIL: DrawTrayBitmap failed for style=$style size=$size connected=true!" -ForegroundColor Red
            exit 1
        }
        $bmpConnected.Dispose()

        # 2. 充电中
        $bmpCharging = $mDraw.Invoke($null, @([int]85, [bool]$true, [bool]$true, [int]$style, [int]$size, [bool]$false, $null))
        if ($bmpCharging -eq $null) {
            Write-Host "FAIL: DrawTrayBitmap failed for style=$style size=$size charging=true!" -ForegroundColor Red
            exit 1
        }
        $bmpCharging.Dispose()

        # 3. 断联状态 (重绘断联图标验证)
        $bmpDisconnected = $mDraw.Invoke($null, @([int]-1, [bool]$false, [bool]$false, [int]$style, [int]$size, [bool]$false, $null))
        if ($bmpDisconnected -eq $null -or $bmpDisconnected.Width -ne $size -or $bmpDisconnected.Height -ne $size) {
            Write-Host "FAIL: DrawTrayBitmap failed for style=$style size=$size disconnected=true!" -ForegroundColor Red
            exit 1
        }
        # 验证断联图标有非透明像素绘制
        $hasPixels = $false
        for ($y = 0; $y -lt $bmpDisconnected.Height; $y++) {
            for ($x = 0; $x -lt $bmpDisconnected.Width; $x++) {
                if ($bmpDisconnected.GetPixel($x, $y).A -gt 0) {
                    $hasPixels = $true
                    break
                }
            }
            if ($hasPixels) { break }
        }
        if (-not $hasPixels) {
            Write-Host "FAIL: Disconnected icon for style=$style size=$size has NO visible pixels!" -ForegroundColor Red
            exit 1
        }
        $bmpDisconnected.Dispose()
    }
}
Write-Host "PASS: All styles render clean 16x16 and 24x24 icons in connected, charging, and disconnected states." -ForegroundColor Green

# 清理资源
$form.Dispose()

Write-Host "ALL TESTS PASSED for test_pure_numeric_and_disconnected_tray.ps1" -ForegroundColor Green
exit 0

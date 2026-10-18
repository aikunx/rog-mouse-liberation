# tests\test_tray_menu_behavior.ps1
# 验证托盘菜单纯净化（零图标/零Emoji/零助记符快捷键）与双轨生命周期（就地设置保活 vs 路由自动关闭）
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

# 1. 检查 ShouldKeepTrayMenuOpen 分类方法契约
$mClassify = $tMain.GetMethod("ShouldKeepTrayMenuOpen", [System.Reflection.BindingFlags]"Public,NonPublic,Static,Instance")
if ($mClassify -eq $null) {
    Write-Host "FAIL: Method ShouldKeepTrayMenuOpen not found in MainForm!" -ForegroundColor Red
    exit 1
}
Write-Host "PASS: ShouldKeepTrayMenuOpen classification method exists." -ForegroundColor Green

# 实例化 MainForm (isAutoStartLaunch = true 保证不弹窗)
$form = [Activator]::CreateInstance($tMain, @([bool]$true))

# 获取私有字段 contextMenu
$fMenu = $tMain.GetField("contextMenu", [System.Reflection.BindingFlags]"NonPublic,Instance")
$contextMenu = $fMenu.GetValue($form)
if ($contextMenu -eq $null) {
    Write-Host "FAIL: Field contextMenu is null" -ForegroundColor Red
    exit 1
}

# 2. 验证纯净化：ShowImageMargin 必须为 false
if ($contextMenu.ShowImageMargin) {
    Write-Host "FAIL: contextMenu.ShowImageMargin is still TRUE! Must be false for zero-icon layout." -ForegroundColor Red
    exit 1
}
Write-Host "PASS: contextMenu.ShowImageMargin is false." -ForegroundColor Green

# 3. 递归遍历所有菜单项，验证零 Emoji、零图像、零 (&X) 助记符
$emojiRegex = "[\uD83C-\uDBFF\uDC00-\uDFFF]|[\u2600-\u27BF]"
$mnemonicRegex = "\(&[A-Za-z]\)"
$allItems = New-Object System.Collections.Generic.List[System.Windows.Forms.ToolStripItem]

function CollectItems($items) {
    foreach ($it in $items) {
        if ($it -is [System.Windows.Forms.ToolStripMenuItem]) {
            $allItems.Add($it)
            if ($it.DropDownItems.Count -gt 0) {
                CollectItems($it.DropDownItems)
            }
        }
    }
}
CollectItems($contextMenu.Items)

foreach ($it in $allItems) {
    if ($it.Text -match $emojiRegex) {
        Write-Host "FAIL: MenuItem '$($it.Text)' contains emoji/icon character!" -ForegroundColor Red
        exit 1
    }
    if ($it.Text -match $mnemonicRegex) {
        Write-Host "FAIL: MenuItem '$($it.Text)' still contains legacy mnemonic shortcut like (&X)!" -ForegroundColor Red
        exit 1
    }
    if ($it.Image -ne $null) {
        Write-Host "FAIL: MenuItem '$($it.Text)' has an Image assigned!" -ForegroundColor Red
        exit 1
    }
}
Write-Host "PASS: All $($allItems.Count) menu items are 100% clean of emoji, images, and legacy mnemonics." -ForegroundColor Green

# 4. 验证双轨行为：类别 A 保持开启，类别 B 允许关闭
$keepOpenCases = @("开机自动启动", "低电量气泡通知", "DPI 切换屏幕提示", "30 秒", "1 分钟", "胶囊电量", "数字百分比")
$closeCases = @("打开控制面板", "立即刷新电量", "设备优先级管理", "手动添加新设备向导", "创建桌面快捷方式", "退出程序")

foreach ($name in $keepOpenCases) {
    $found = $null
    foreach ($it in $allItems) {
        if ($it.Text -like "*$name*") { $found = $it; break }
    }
    if ($found -ne $null) {
        $shouldKeep = $mClassify.Invoke($form, @($found))
        if (-not $shouldKeep) {
            Write-Host "FAIL: In-place setting '$($found.Text)' was NOT marked as KeepOpen!" -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host "PASS: Category A in-place settings correctly classified as KeepOpen." -ForegroundColor Green

foreach ($name in $closeCases) {
    $found = $null
    foreach ($it in $allItems) {
        if ($it.Text -like "*$name*") { $found = $it; break }
    }
    if ($found -ne $null) {
        $shouldKeep = $mClassify.Invoke($form, @($found))
        if ($shouldKeep) {
            Write-Host "FAIL: Navigation item '$($found.Text)' was mistakenly marked as KeepOpen!" -ForegroundColor Red
            exit 1
        }
    }
}
Write-Host "PASS: Category B navigation/action items correctly classified as CloseOnAction." -ForegroundColor Green

Write-Host "ALL TESTS PASSED for test_tray_menu_behavior.ps1" -ForegroundColor Green
exit 0

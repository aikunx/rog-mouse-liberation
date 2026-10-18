# 01 · 安装包结构逆向（ROG STRIX IMPACT III WIRELESS v1.0.2.2）

> 分析对象：`D:\下载\ROG_STRIX_IMPACT_III_WIRELESS\ROG_STRIX_IMPACT_III_WIRELESS\ROG STRIX IMPACT III WIRELESS v1.0.2.2`
> 方法：PowerShell 直读 ini/文本、`[Diagnostics.FileVersionInfo]` 读 PE 版本、`Get-FileHash`、字节级 hex dump、UTF-16/ASCII 字符串提取、读注册表与已装目录做对照。
> 全程只读，未解包、未执行任何 exe。

---

## 0. 结论速览

| # | 结论 | 依据 |
|---|---|---|
| 1 | 安装包是 **18 个独立安装器的离线聚合包**，由根目录 `ArmouryCrateGearInstall.exe`（1.0.3.0）按 `Config.ini` 的 `Item_1..Item_18` 顺序**串行**调用 | `Config.ini` 全文；`ArmouryCrateGearInstall.exe` UTF-16 串 `[AC Installer] Install file (`、`Item_Path`/`Item_Name`/`Item_Param`/`Item_Ver`/`Item_Reg`/`Item_RegKey` |
| 2 | 安装器类型共 6 种：InstallShield 4 处、ASUS 自研 HttpSetup 7 处、WebAC Html Setup 2 处、FWUpdateSetup 2 处、WiX Burn 1 处、微软 vc_redist 2 处 | 各分包的 `ISSetup.dll`/`setup.inx`/`data1.cab`/`Setup.iss` 与 PE 版本信息 |
| 3 | 全部文件只落到 3 个物理目录 + 1 个注册表树：`C:\Program Files (x86)\ASUS\ArmouryDevice`、`C:\Program Files\ASUS\Aac_Mouse`、`C:\Program Files (x86)\InstallShield Installation Information\{GUID}`、`HKLM\SOFTWARE\WOW6432Node\ASUS\*` | `SdkSetup.exe`/`FWUpdateSetup.exe` 字符串 `Installation location` + `%s\dll\`、`%s\Firmware\%s`；注册表实测值与 `Setup.ini` 的 GUID/Version 一一对应 |
| 4 | 设备页面是 **6 个独立 HttpSetup 分包**（p520/6871/6873/omni/6862 + 2 个父级聚合器），统一装到 `ArmouryDevice\View\<PID>\` | `Device\p520\{p520,6871,6873}`、`Device\omni\{omni,6862}` 各含 `Setup.exe`(1.0.2.9 HttpSetup)+`Setup.ini`；已装目录 `ArmouryDevice\View\{p520,6871,6873,omni,6862}` |
| 5 | `Framework\install\data2.cab`（123.9 MB）承载 Electron 框架主体 `asus_framework.exe`+`resources`，**无法直接枚举**（InstallShield 私有 `ISc(` 容器，本机无 7z，`expand -D` 拒绝） | `data2.cab` 头部 `49 53 63 28`；`FrameworkFileList.ini` 声明 19 文件/6 目录；已装 308 MB 对应压缩后 123.9 MB |
| 6 | 固件 bin 是 **ARM Cortex-M 裸机镜像**（偏移 0 处为向量表：初始 SP + Reset 向量），不是打包容器 | `P520_MOUSE_V04_00_02.bin` 头 `00 04 00 20 81 0A 00 00` → SP=0x20000400，Reset=0x00000A81(Thumb) |
| 7 | MultiLang 实为 **23** 个语言 ini（任务书写的 24 与实测不符） | 目录实测 23 个文件；`ArmouryCrateGearInstall.exe` 内 UTF-16 串也正好列 23 个 `\MultiLang\*.ini` |

---

## 1. 顶层目录树（深度 3）

体积为本机实测递归值（文件数含全部子目录）。

~~~text
ROG STRIX IMPACT III WIRELESS v1.0.2.2\        总计 ≈ 323 MB
├─ ArmouryCrateGearInstall.exe   1.83 MB     主安装器（WPF/.NET，总控）
├─ Config.ini                    5,450 B     安装清单：18 个 Item 的路径/参数/版本/注册表锚点
├─ VersionList.txt               331 B       版本对照表（人读用）
├─ WebACLog.dll                  270 KB      日志 DLL（HttpSetup/WebAC 系列共用）
│
├─ Device\                       91.72 MB / 1582 files
│  ├─ p520\                      70.48 MB      ROG STRIX IMPACT III WIRELESS（本机主设备）
│  │  ├─ Setup.exe               4.70 MB       WebAC Html Setup 1.0.3.1（设备级聚合器）
│  │  ├─ HomePage.exe            5.03 MB       设备主页壳
│  │  ├─ Shortcut.exe            1.49 MB       快捷方式创建器
│  │  ├─ DeviceUninstall.dll     68.9 KB       卸载逻辑
│  │  ├─ Setup.ini               874 B        [HomePage]CMD=/lib/1/window/p520、[Uninstall]2 个 GUID
│  │  ├─ p520\  9.18 MB / 110 files   → 有线/通用页（"p520" 模板）
│  │  ├─ 6871\  25.00 MB / 622 files  → 2.4GHz(DG) 页
│  │  └─ 6873\  25.01 MB / 626 files  → 蓝牙(BT) 页
│  └─ omni\                      21.25 MB / 219 files  ROG OMNI RECEIVER（接收器本体页）
│     ├─ Setup.exe/HomePage.exe/Shortcut.exe/DeviceUninstall.dll/Setup.ini
│     ├─ omni\  4.70 MB / 88 files
│     └─ 6862\  5.25 MB / 126 files
│
├─ Firmware\                     17.65 MB / 23 files
│  ├─ Bin\                       6.89 MB / 17 files   → 装到 ArmouryDevice\Firmware\
│  │  ├─ FWUpdateSetup.exe       4.68 MB      1.0.4.0（自研，复制型安装器）
│  │  ├─ Setup.ini               121 B        Name=P520_Firmware, Ver=1.0.0.1
│  │  └─ Firmware\{6862,6871}\                接收器 / 鼠标两套固件与升级 exe
│  └─ Tools\                     10.76 MB / 6 files  → 装到 ArmouryDevice\Firmware\Tools\
│     ├─ FWUpdateSetup.exe       4.68 MB      1.0.4.0（同一个 exe，另一份 Setup.ini）
│     ├─ Setup.ini               130 B        Name=FWUpdateTool, Ver=1.0.0.4
│     └─ Tools\{GetDeviceVersion.exe, MsFWUpdate.dll, cpprest141_2_10.dll, ROGWeChat.exe}
│
├─ Framework\                    128.30 MB / 21 files   Armoury Crate 框架服务（Electron）
│  ├─ FrameworkServiceSetup.exe  200 KB        3.0.0.3（控制台包装器，要求管理员）
│  ├─ config.ini / English.ini / FrameworkFileList.ini / Process.ini / RLSSetting.ini / Setup.iss
│  ├─ CheckSumHelper.dll         270 KB
│  └─ install\                   127.84 MB / 13 files  InstallShield 工程实体
│     ├─ setup.exe               890 KB        InstallScript Setup Launcher Unicode 3.0.0.4
│     ├─ data1.cab               1.11 MB       ISc( 私有容器
│     ├─ data1.hdr               22.9 KB       ISc( 私有容器
│     ├─ data2.cab               123.90 MB     ← 本包最大文件，Electron 主体
│     ├─ ISSetup.dll             1.53 MB       30.0.157
│     ├─ setup.inx               220 KB        InstallShield 编译脚本
│     ├─ setup.ini / 0x0409.ini / layout.bin / setup.log / Setup.iss / config.ini / VersionHelper.dll
│
├─ MultiLang\                    0.04 MB / 23 files   安装器 UI 语言包（23 种语言）
│
├─ SDK_HAL\                      48.39 MB / 72 files
│  ├─ FWMSPlugin\                3.76 MB  / 8 files    → ArmouryDevice\modules\mouse
│  ├─ MainSDK\                   19.61 MB / 19 files   → ArmouryDevice\dll
│  ├─ MouseHal\                  4.50 MB  / 3 files    → C:\Program Files\ASUS\Aac_Mouse
│  ├─ MouseSDK\                  8.83 MB  / 18 files   → ArmouryDevice\dll\MouseSDK
│  ├─ unifyDonglePlugin\         3.59 MB  / 6 files    → ArmouryDevice\modules\unifyDongle
│  └─ UnifyDongleSDK\            8.09 MB  / 18 files   → ArmouryDevice\dll\UnifyDongleSDK
│
└─ Tools\                        37.45 MB / 2 files
   ├─ vc_redist.x64.exe          24.25 MB    14.38.33130.0
   └─ vc_redist.x86.exe          13.20 MB    14.38.33130.0
~~~

> 依据：`Get-ChildItem -Recurse -File | Measure-Object Length -Sum`；各 ini 的 `[Device]Name/Version` 与 `Config.ini` 的 `Item_*` 同值。

### 1.1 三份清单文件的关系

- `Config.ini`：**唯一权威安装清单**（18 条），含 `Item_Path / Item_Name / Item_Param / Item_Ver / Item_Reg / Item_RegKey`。
- `VersionList.txt`：同 18 项的人类可读版本摘要，与 `Config.ini` 的 `Item_Ver` 一致，例如 `Framework: v4.2.2.8`、`MouseSDK v3.01.48`、`MsFWUpdate v1.0.0.37`。
  **注意**：`VersionList.txt` 写 `FrameworkMSPlugin: v2.4.3`、`Config.ini` 写 `Item_Ver4 = 2.4.3`，但 `SDK_HAL\FWMSPlugin\Setup.ini` 实为 `Version = 2.3.4`，注册表实装值也是 `2.3.4` —— **主清单版本号与实际分包不一致**（差一个小版本）。

---

## 2. 主安装器 ArmouryCrateGearInstall.exe

| 属性 | 值 | 依据 |
|---|---|---|
| 文件版本 / 产品名 | 1.0.3.0 / `Armoury Crate Gear Installer` | `FileVersionInfo` |
| 技术栈 | .NET + WPF（`programwindow.xaml`） | 字符串 `ArmouryCrateGearInstall.Properties.Resources`、`/ArmouryCrateGearInstall;component/programwindow.xaml`、`ProcessWindowStyle`、`IWshShortcut` |
| 构建路径 | `D:\Jenkins\workspace\ArmouryCrateWeb install\Installer\FWUpdater\obj\x86\Release\ArmouryCrateGearInstall.pdb` | PDB 路径字符串 |
| 配置源 | `\Config.ini` | 字符串 `\Config.ini`、`[AC Installer] Config file = ` |
| 语言包 | `\MultiLang\<Language>.ini`（硬编码 23 个语言名） | 23 条 UTF-16 字符串 `\MultiLang\*.ini` |
| 版本闸门 | 读目标机注册表版本，`VersionCompare` 决定装/跳过 | 字符串 `[AC Installer][VersionCompare] can install OOO.` / `can't install XXX.`、`LocalVer: ` |
| 收尾动作 | 启动 `ArmourySocketServer.exe`（框架的本地 socket 服务） | 字符串 `[AC Installer]  execute  ArmourySocketServer.exe Done.`、`GetSocketServerPath` |
| 短路条件 | 检测到 **Armoury Crate Lite** 存在则拒绝安装 | 字符串 `ArmouryCrateLiteInstaller` / `Armoury Crate Lite Installer` / `Please go to Windows Settings > Apps to uninstall Armoury Crate Lite...` |
| 其它 | 创建桌面/开始菜单快捷方式、写 `\shortcut.lnk`、识别 `\View\` 目录 | 字符串 `CreateShortcut`、`\shortcut.lnk`、`\View\`、`[AC Installer] create shorcut OK.` |

界面提示串（`MultiLang\English.ini` 原文，可作为 UI 文案契约）：
`{0} Installer` / `Install {0} app to set device configurations.` / `Version: v{0}` / `Installation Completed` / `Installation Failed` / `Restart Now` / `Restart Later` / `Launch {0} to start device setting.`

---

## 3. Device 页面分包：p520 / 6871 / 6873 / omni / 6862

### 3.1 嵌套结构

每套页面都是 **`<PID>\<PID>\` 双层同名目录**，页面内容在第二层，`resources\` 在其下：

~~~text
Device\p520\
├─ Setup.exe / HomePage.exe / Shortcut.exe / DeviceUninstall.dll / Setup.ini   ← 设备级（WebAC Html Setup 1.0.3.1）
├─ p520\p520\    Setup.exe(HttpSetup 1.0.2.9) + CheckSumHelper.dll + Setup.ini ← 页面级安装器
│  └─ resources\                                                             ← 页面资源
├─ 6871\6871\    （同上）
└─ 6873\6873\    （同上）
~~~

**双层同名的用途（推断）**：第一层 `p520\` 是"家庭/安装单元"（含 1 个 HttpSetup + 校验 DLL + `Setup.ini` 声明 `Name=p520`），第二层 `p520\p520\` 才是待复制负载；HttpSetup 以 `Setup.ini` 的 `Name` 决定目标子目录，因此出现同名双层。

### 3.2 三套页面体量与内容差异

| 指标 | `p520\p520`（通用/有线） | `6871\6871`（2.4GHz） | `6873\6873`（蓝牙） |
|---|---|---|---|
| 递归文件数 | 110 | 622 | 626 |
| 递归体积 | 9.18 MB | 25.00 MB | 25.01 MB |
| 页面根文件 | 46（`index.js` 675,883 B、`index.html`、`config.ini`、`MD5.ini`、36 个 `*-bundle-3ca91.{js,css}`） | 20（`index.js` 903,964 B、`index.css`、`service.js` 621,724 B、`MD5.ini` 112,053 B、9 个 `*-bundle-e147c.*`） | 24（`index.js` 903,961 B、`service.js` 621,137 B、`MD5.ini` 116,450 B、**13** 个 `*-bundle-fb271.*`，比 6871 多 `235/723/724/831-bundle-fb271.js`） |
| 构建哈希后缀 | `3ca91` | `e147c` | `fb271` |
| `resources` 递归 | 61 files / 4.84 MB | 599 files / 18.70 MB | 599 files / 18.70 MB |
| 语言 xml | 24（`English-68efc.xml` … `Thai-46703.xml`，139–300 KB/个） | **48**（24 在 `resources\` 根 + 24 在 `resources\node_modules\ac-components\src\_ref\language\`，后者合计 4.11 MB） | 同 6871 |
| 图标 | 11 svg + 2 png + 1 gif（`p520_pic-cf973.svg`、`main_bg-cac7e.png` 460 KB 等） | 441 svg + 80 png（含全套灯效图标 `rainbow/ripple/wave/…`、`profile1..5` 系列、`icn_calibration_l.png` 570 KB、`main_bg-cac7e.png` 460 KB） | 同 6871（同名集，仅个别 json 字节数差 1 KB） |
| 字体 | — | `ROGFonts-Regular.{ttf,otf,woff,eot}` | 同 6871 |
| `resources\src\_ref\keyDefinitions` | 22 个 json（`caps.json`、`en-US.json`、`de-DE.json`…，含 `_mac` 变体） | 22 个 json | 22 个 json |
| 键盘布局图 | 无 | `layout104/105/107_*.svg` 41 个（阿拉伯/希伯来/泰/日/英/德…） | 同 6871 |
| `config.ini` 关键键 | `DeviceType=1 APIVersion=2 ServiceVersion=1 UWPVersion=1 ReloadS3=1 ArmourySocketServer=true` | 同上 + `DG=6871` `BT=6873` | 同 6871 |

> 依据：三目录 `Get-ChildItem -Recurse`（分组统计扩展名）+ 各 `Setup.ini`/`config.ini` 全文 + `MD5.ini` 头。
> `MD5.ini` 泄露了产线构建路径：`D:\Workspace\ArmouryCrate\AC_Frontend_Mouse\view\Web AC - ROG STRIX IMPACT III WIRELESS\Web AC - ROG STRIX IMPACT III WIRELESS\p520\...`，`[FilesToCheck] Count=105`（p520 页面共 105 个受校验文件）。

**结论**：三套页面同源（同一套 React 构建产物），差异在 ① bundle 哈希与切片编号 ② 6871/6873 额外带 `service.js`（后台服务层）和整套键位/灯效图标库 ③ `config.ini` 的 `DG/BT` PID 绑定。`p520` 是"通用 + 有线"精简页，`6871`/`6873` 是同一页面的 2.4GHz / 蓝牙两种连接态变体。

### 3.3 设备级 Setup.ini 的注册与卸载关系

`Device\p520\Setup.ini`（874 B 全文要点）：

~~~ini
[Device]
Version=1.0.2.2
Name=p520
GUID={c0bfe97f-154d-4a56-a88c-984378f29ac8}
DisplayName=ROG STRIX IMPACT III WIRELESS
DisplayNameML=ROG 影刃 3 无线版
[HomePage]
CMD = /lib/1/window/p520
[Uninstall]
count = 2
1={2e3ccb5f-07b8-481b-90e2-f4165a3ccd21}     ; = 6871 页面 GUID
2={603cdb29-461b-42d5-98c3-58fd30e5f4df}     ; = 6873 页面 GUID
~~~

即：**父级（p520）注册表项 = 设备登记 + 主页路由；两个子页面各自持有独立 GUID 并在父级登记为"一并卸载"**。子页面 `Setup.ini` 分别声明 `DisplayName=…(2.4GHz)` / `…(Bluetooth)`、`Version=4.05.38`。
`Device\omni\Setup.ini` 同构（`GUID={7620362f-…}`，`[Uninstall]1={c8296bac-…}` = 6862 页面）。

---

## 4. SDK_HAL 六个分包

### 4.1 总表

| 分包 | 体积/文件 | 主安装器（版本） | 声明版本 | 注册表锚点 `HKLM\SOFTWARE\WOW6432Node\ASUS\` | 实装位置（已核对） |
|---|---|---|---|---|---|
| `FWMSPlugin` | 3.76 MB / 8 | `Setup.exe`（HttpSetup 1.0.2.9, 3.30 MB） | 2.3.4 | `ArmouryFramework\mouse` | `ArmouryDevice\modules\mouse\` |
| `MainSDK` | 19.61 MB / 19 | `SdkSetup.exe`（2.0.0.3, ASUS SDK）+ `Setup.exe`（1.00.0000, InstallScript） | 2.01.40 | `ArmourySDK\MainSDK` | `ArmouryDevice\dll\` |
| `MouseHal` | 4.50 MB / 3 | `AacMouseSetup.exe`（1.0.0.2 启动器）→ `Hal\AacMouseSetup.exe`（1.2.1.18, WiX Burn） | 1.2.1.18 | `Hal\{68AB32E8-…}`（**实测不存在**） | `C:\Program Files\ASUS\Aac_Mouse\` |
| `MouseSDK` | 8.83 MB / 18 | `SdkSetup.exe`（1.0.0.28）+ `Setup.exe`（1.00.0000, `AC_MsSDK`） | 3.01.48 | `ArmourySDK\MouseSDK` | `ArmouryDevice\dll\MouseSDK\` |
| `unifyDonglePlugin` | 3.59 MB / 6 | `Setup.exe`（HttpSetup 1.0.2.9） | 0.1.1 | `ArmouryFramework\unifyDongle` | `ArmouryDevice\modules\unifyDongle\` |
| `UnifyDongleSDK` | 8.09 MB / 18 | `SdkSetup.exe`（1.0.0.25）+ `Setup.exe`（1.00.00, `UnifyDongleSDK`） | 1.00.10 | `ArmourySDK\UnifyDongleSDK` | `ArmouryDevice\dll\UnifyDongleSDK\` |

### 4.2 三类安装器各自的元数据文件

**(A) HttpSetup 系列（FWMSPlugin、unifyDonglePlugin、全部 Device 页面）** —— 有 `Setup.ini` + `CheckSumHelper.dll` + `VersionHelper.dll`，无 cab。

`SDK_HAL\FWMSPlugin\Setup.ini` 全文：

~~~ini
[Device]
Version = 2.3.4
Name = mouse
GUID = {40fb7bea-7244-4bcd-bdac-ab241f113a18}
DisplayName = Armoury Crate Plugin - Mouse
TargetFolder = modules        ; ← 目标为 ArmouryDevice\modules\<Name>
ShowAP = 0
RegKey = ArmouryFramework
[ChcekSumSkipFiles]
Count = 1
[1]
FileName = MD5.ini
[Process]
Count = 2
0 = ArmourySocketServer.exe
1 = asus_framework.exe
~~~

- `[Process]` = 安装前后需要停掉的进程清单（这里是框架的两个进程），装完由主安装器重新拉起。
- `[ChcekSumSkipFiles]`（原文拼写如此，非笔误）声明不做校验和比对的例外文件。
- `mouse\MD5.ini`：`[FilesToCheck]Count=2`，构建路径 `D:\Workspace\ArmouryCrate\AC_Backend_Mouse\build\mouse\mouse\index.js`，`[MD5]Value=2304261245`（日期式版本戳 23-04-26 12:45）。
- `RLSSetting.ini`（RogueWare Launch Setting，所有包统一 `[INIVERSION]value=3.0.0`）中 `[APCategory]value=Framework`、`[FileName]value=Setup.exe`、`[Arguments]value=-s`、`[Timeout]value=60000`、`[ReturnCode]value=0,255`、`[RebootCode]value=255`；卸载走 `[UninstallFileName]value=Registry` + `[UninstallRegPath]value=SOFTWARE\WOW6432Node\ASUS\ArmouryFramework\` + `[UninstallArguments]value=-s -uninstall`。→ **HttpSetup 自身不写卸载程序，卸载靠注册表锚点 + 子安装器的 `-uninstall`**。
- `VersionHelper.dll`（1.0.0.7）在 FWMSPlugin 与 unifyDonglePlugin 中都存在，而 SDK 系列为 1.0.0.13 / Framework 为 1.0.0.16 —— 版本随包递增。

**(B) InstallShield 系列（MainSDK / MouseSDK / UnifyDongleSDK）** —— 结构完全一致：

| 文件 | MainSDK | MouseSDK | UnifyDongleSDK |
|---|---|---|---|
| `Setup.exe` | 890 KB, 1.00.0000, `AC_MainSDK`（InstallScript Launcher） | 890 KB, `AC_MsSDK` | 890 KB, `UnifyDongleSDK` |
| `SdkSetup.exe` | 180 KB, 2.0.0.3, `ASUS SDK` | 3.30 MB, 1.0.0.28 | 3.30 MB, 1.0.0.25 |
| `ISSetup.dll` | 1.57 MB, **29.0.111** | 1.53 MB, **30.0.157** | 1.53 MB, **30.0.157** |
| `data1.cab` / `data1.hdr` | 1.10 MB / 60 KB | 1.11 MB / 70 KB | 1.11 MB / 10 KB |
| `data2.cab` | **15.16 MB** | 1.28 MB | 0.60 MB |
| `setup.inx` / `0x0409.ini` | 220 KB / 22.9 KB | 同 | 同 |
| `Setup.iss`（响应文件） | `Name=AC_MainSDK Version=1.00.0000` | `AC_MsSDK` | `UnifyDongleSDK` |
| `setup.log`（静默安装日志模板） | `ResultCode=0` | 同 | 同 |
| `config.ini` | `Version =2.01.40 GUID={6EE02C78-E908-493B-B1A6-D64AFC53002F}` | `3.01.48 {19FFD388-F7D7-4574-8C91-A9C40CDD9AA7}` | `1.00.10 {7F9DEA9C-8FC3-4AAB-BB05-1F41BB1CA4A8}` |
| `English.ini` | `Title = ArmourySDK` | 同 | 同 |

- 三份 `0x0409.ini`（22,914 B）**SHA256 完全相同**（`CDB316D7F9AA…583D29`），也与 `Framework\install\0x0409.ini` 相同 → 是 InstallShield 通用英文串表（`1100=Setup Initialization Error` … `2161=The digital signature of Setup.exe doesn't match...`），不含产品信息。
- `data1.cab` 四包体量都在 1.10–1.11 MB，但 SHA256 各不相同；`data2.cab` 则随产品从 0.60 MB 到 123.9 MB 剧烈变化 → **推断 `data1.cab` = InstallShield 引导/脚本层，`data2.cab` = 产品负载**（无法解开 `ISc(` 私有容器，标**未验证**）。
- `MainSdkFileList.ini`（**仅 MainSDK 有**）：
  - `[File]count=5`：`ArmourySDK.dll`、`config.ini`、`InstallHelper.dll`、`Process.ini`、`MD5.ini`
  - `[Folder]count=6`：`FilterDriver`、`SwAgent`、`ArmourySocketServer`、`Tools`、`ShareFromArmouryIII`、`AcPowerNotification`
- `MainSDK\Process.ini`：`Count=7`（`ArmourySwAgent.exe`、`FanOSD.exe`、`ArmourySocketServer.exe`、`asus_framework.exe`、`P508PowerAgent.exe`×2、`AcPowerNotification.exe`）+ `[Startup]2` + `[UninstallHelper]1 = \ArmourySocketServer\PreUninstallArmourySocketServer.exe`。
- `SdkSetup.exe` 字符串证明落点：`%s\dll\`、`%s\dll\%s`、`Installation location`、`%s\MainSdkFileList.ini`、`InstallShield: 5017 Issue....re-install`（5017 = 已安装更高版本，会先反装再装）。
- `RLSSetting.ini`：`[APCategory]value=MainSDK`（MouseSDK/UnifyDongleSDK 为 `SDK`）、`[Timeout]value=300000`、卸载 `UninstallFileName=Registry` + `UninstallRegPath=SOFTWARE\WOW6432Node\ASUS\ArmourySDK\MainSDK\` + `[UninstallArguments]value=-uninstall -s`。
- `MouseSDK\Process.ini`：`Count=4`（`ArmourySocketServer.exe`、`asus_framework.exe`、`dexin.exe`、`MonitorDetect.exe`），`[Startup]Count=0`，`[UninstallHelper]Count=0`（与 MainSDK 不同，MouseSDK 不装驻留进程）。

**(C) WiX Burn 系列（MouseHal）**：

- `SDK_HAL\MouseHal\AacMouseSetup.exe`（120 KB, 1.0.0.2, `ASUS_LS_Hal_IInstaller`）只是启动器，真实载荷在 `Hal\AacMouseSetup.exe`（4.39 MB, 1.2.1.18, `ASUS Mouse HAL`）。
- 载荷内嵌 WiX Burn 引擎：字符串 `c:\agent\_work\36\s\wix\src\burn\engine\engine.cpp`、`.wixburn`、`Failed to parse MSI package.` → **它是 MSI/Burn 引导器**，MSI 包在 exe 资源里（未解包，标**未验证**内容明细）。
- `RLSSetting.ini`：`[APCategory]value=HAL`、`[Arguments]value=/quiet /norestart`、`[Timeout]value=180000`、`[ReturnCode]value=0,3010`、`[RebootCode]value=none`；`[Uninstall*]` 全为 `none` → **HAL 不提供 ASUS 侧卸载入口**（靠 MSI 自身 ARP 项）。
- 实装只在 `C:\Program Files\ASUS\Aac_Mouse\` 落了两个 DLL：`AacMouseHal_x64.dll`（2.85 MB）与 `AacMouseHal_x86.dll`（3.36 MB），版本均 1.2.1.18；该目录下无 exe、无配置文件。

---

## 5. Framework（框架服务）

### 5.1 文件构成与角色

| 文件 | 用途 | 依据 |
|---|---|---|
| `FrameworkServiceSetup.exe`（200 KB, 3.0.0.3, `ASUS Framework`） | **控制台包装器**：读 FrameworkFileList.ini → 执行 `install\Setup.exe` → 处理 InstallShield 5017（已装更高版本时先反装）→ 复制自身与 `config.ini`/`Process.ini`/`FrameworkFileList.ini` 到安装目录 | 内嵌 manifest `requestedExecutionLevel level="requireAdministrator"`；字符串 `%s\FrameworkFileList.ini`、`%s\install\Setup.exe`、`[Install] InstallShield: 5017 Issue....re-install..%d`、`[Install] Copy File(FrameworkServiceSetup.exe, config.ini, Process.ini)`、`[EnumFrameworkRegInfo] UninstallCmd = %s` |
| `install\setup.exe`（890 KB, 3.0.0.4） | 真正的 InstallScript 安装引擎（`InstallScript Setup Launcher Unicode`） | PE 版本 + `setup.inx` 存在 |
| `install\setup.inx`（220 KB） | InstallShield 编译脚本（`t.,....(` 魔数头，不可读） | 头部 hex |
| `install\ISSetup.dll`（1.53 MB, 30.0.157） | InstallShield 引擎 DLL | PE 版本 |
| `install\data1.cab`（1.11 MB）+ `data1.hdr`（22.9 KB） | ISc( 私有容器，头字符串不可读（ASCII 扫描仅 2 条噪声串） | `49 53 63 28` 签名 |
| `install\data2.cab`（**129,918,960 B = 123.90 MB**） | Electron 框架主体负载 | 见 5.2 |
| `FrameworkFileList.ini` | 安装内容契约：`[File]count=19`、`[Folder]count=6` | 全文见下 |
| `config.ini` | `Version =4.2.2.8`、`Name = FrameworkServiceSetup`、`GUID = {339A6383-7862-46DA-8A9D-E84180EF9424}` | 全文 |
| `Setup.iss` | 响应文件：`Name=ASUS Framework Service  Version=3.0.0.4  Company=ASUS` | 全文 |
| `Process.ini` | 8 个进程 + 2 个自启动 + 1 个卸载前钩子（比 MainSDK 多 `ArmouryAudioAgent.exe`） | 全文 |
| `RLSSetting.ini` | `[APCategory]value=Framework`、`/quiet /norestart`、timeout 180000、return `0,3010`、reboot 3010；卸载 `UninstallRegPath=SOFTWARE\WOW6432Node\ASUS\ArmouryDevice`、`[UninstallArguments]=/quiet /uninstall` | 全文 |

`FrameworkFileList.ini` 全文：

~~~ini
[File]
count = 19
0 = asus_framework.exe            6 = ffmpeg.dll             12 = resources.pak
1 = asus_framework.exe.manifest   7 = icudtl.dat             13 = snapshot_blob.bin
2 = asus_framework.VisualElementsManifest.xml  8 = libEGL.dll  14 = v8_context_snapshot.bin
3 = chrome_100_percent.pak        9 = libGLESv2.dll          15 = version
4 = chrome_200_percent.pak       10 = LICENSE                16 = vk_swiftshader.dll
5 = d3dcompiler_47.dll           11 = LICENSES.chromium.html 17 = vk_swiftshader_icd.json
                                                            18 = vulkan-1.dll
[Folder]
count = 6
0 = node_modules   1 = ssl   2 = locales   3 = resources   4 = dll\Schedule   5 = dll\WindowID
~~~

### 5.2 data2.cab 承载什么

- 无法直接枚举：签名是 InstallShield 自己的 `ISc(`（`49 53 63 28 B8 0B 00 04 …`），不是微软 `MSCF`；`expand.exe -D` 只回显文件名、不列条目；本机无 `7z`。→ **容器内容标未验证**。
- 用体量反推（**推断**）：`FrameworkFileList.ini` 声明的 19 文件 + `resources\` 在已装目录实测 = `asus_framework.exe` 152,079,704 B（145 MB）+ `LICENSES.chromium.html` 9.3 MB + `icudtl.dat` 10.7 MB + d3d/ffmpeg/GL/vulkan 等约 22 MB + `resources.pak` 5.3 MB + `resources\app.asar` 28.4 MB + `resources\app.asar.unpacked` 79 MB ≈ **308 MB**；压到 123.9 MB 约 2.5:1，与 data2.cab 体量吻合。
- 因此结论：**`data2.cab` = Electron 运行时 + `asus_framework.exe` + `resources\app.asar`（Armoury Crate 前端/后端 JS 包）**；`data1.cab` = 同样 1.1 MB 的引导层。

### 5.3 与"框架服务"的关系

- 安装目标由注册表 `HKLM\SOFTWARE\WOW6432Node\ASUS\ArmouryDevice` 的 `Installation location = C:\Program Files (x86)\ASUS\ArmouryDevice` 给出（`FrameworkServiceSetup.exe` 字符串 `Installation location`）。
- 装完后框架侧关键文件：`ArmouryDevice\asus_framework.exe`（145 MB）、`ArmouryDevice\version`（内容 `29.4.6`）、注册表 `ArmouryDevice\Version = 4.2.2.8`、`HTTPPort=1042`、`HTTPSPort=1043`。
  → **两个版本号含义不同**：`version` 文件 `29.4.6` 是 Armoury Crate 应用版本，注册表 `4.2.2.8` 是本安装包（Framework 分包）版本。
- `FrameworkFileList.ini` 声明的 6 个目录中，**本次 Gear 安装实测只有 `resources`、`locales`、`dll\Schedule` 存在**；`node_modules`、`ssl`、`dll\WindowID` 未生成（`Test-Path` = False） → 推断这些目录只在完整版 Armoury Crate 下创建，Gear 版裁剪。

---

## 6. Firmware（固件与升级工具链）

### 6.1 两个固件分包

| 分包 | `Setup.ini` | 安装目标 |
|---|---|---|
| `Firmware\Bin` | `Version=1.0.0.1`、`Name=P520_Firmware`、`GUID={4827D8C9-13A7-4F22-A4C8-DEAF8A99CC90}`、`DisplayName=P520 Firmware` | `ArmouryDevice\Firmware\` |
| `Firmware\Tools` | `Version=1.0.0.4`、`Name=FWUpdateTool`、`GUID={84107401-2D3A-4F9C-23AB-D959AE2471AA}`、`DisplayName=Firmware Update Tools` | `ArmouryDevice\Firmware\Tools\` |

`FWUpdateSetup.exe` 两处是**同一个文件**（4,908,904 B，1.0.4.0），靠所在目录的 `Setup.ini` 区分安装内容。其字符串给出复制逻辑：

~~~text
%s\Setup.ini                      ← 读本目录清单
SOFTWARE\ASUS\ArmouryDevice       ← 读 Installation location
%s\Firmware                       ← 目标根
%s\Firmware\%s                    ← 目标 = ...\Firmware\<Name>
%s\Firmware\*.*                   ← 卸载时整目录清空
======= Start Uninstall ======== / >>>>>>>> Start  Install <<<<<<<<
Install-iCurrentVersion:%d ; iRegVersion : %d
~~~

### 6.2 接收器 6862（ROG OMNI RECEIVER）

`Firmware\Bin\Firmware\6862\config.ini` 全文：

~~~ini
[USB Info]
Vid = 0x0b05
Pid = 0x1ACE
Usage = 0x01
UsagePage = 0xff02
;Type
;0 : Unify Dongle
;1 : Keyboard
;2 : Mouse
Device Type = 0

[FW Info]
Bin Name = OMNI_RECEIVER_V06_00_25.bin
Cmd 1 = m 1ACE 1ACF 48 64 FF02 FF01 4 
Cmd 2 = ASFW:n UA:y SN:y
Executable file = peripheral_fwu_pro.exe
Version = 6.00.25

[Parameters]
Success Code = 255
Ignore Error = 12
Wait for ready = 6000
~~~

`Dependence.ini`：`HTML=0 SDK=0 HAL=0 FrameworkMSPlugin=0`、`cmd=FW_Update_Tool_Unify_Dongle.exe -s`
→ 四类依赖全 0，表示**该固件升级不依赖页面/SDK/HAL/插件**，只要 `FW_Update_Tool_Unify_Dongle.exe` 即可独立跑。

### 6.3 鼠标 6871（P520 MOUSE）

`Dependence.ini`：同 4 个 0，`cmd=My_FW_Update_P520_Mouse.exe -s`

`version.ini` 全文：

~~~ini
[Version]
Vid=2821                  ; = 0x0B05（十进制写法）
Pid=6862                  ; 注意：鼠标的固件通道挂在接收器 PID 上
BootloaderPid=6863
MCUCount=1
Complete=4.00.02
Code=255

[MCU_1]
CMD=s 1ACE 1ACF 112 248 FF01 FF01 1
Version=P520_MOUSE_V04_00_02.bin 
CMD_2=1AD7 UA:y \npause
Number=0
~~~

- `Complete=4.00.02` 与注册表 `ArmouryHttp\6871\FWVersion = 4.00.02` 一致 → **当前鼠标固件 = V04_00_02**。
- 注册表同键还有 `FWDGVersion = 7.00.08`（接收器固件），**高于本包携带的 `OMNI_RECEIVER_V06_00_25.bin`（6.00.25）** → 推断升级器只做"更高版本才刷"，不会把 6.00.25 刷回已装 7.00.08 的设备（需实测确认，标**未验证**）。

### 6.4 固件 bin 大小与版本对应

| 文件 | 大小 | 声明版本 | 位置 |
|---|---|---|---|
| `P520_MOUSE_V03_00_02.bin` | 520,192 B（0x7F000） | V03_00_02（旧版/回滚） | `Firmware\Bin\Firmware\6871\` |
| `P520_MOUSE_V04_00_02.bin` | 520,192 B（0x7F000） | V04_00_02（**`version.ini` 指定刷入**） | 同上 |
| `OMNI_RECEIVER_V06_00_25.bin` | 253,952 B（0x3E000） | 6.00.25 | `Firmware\Bin\Firmware\6862\FW\` |

V03 与 V04 逐字节差异 **198,246 B（38.11%）**，SHA256：
`4B094295…43A018`（V03） / `C1104F10…F53528`（V04）。

### 6.5 固件格式判定（前 256 字节）

`P520_MOUSE_V04_00_02.bin`：

~~~text
00 04 00 20 81 0A 00 00 15 07 00 00 61 0A 00 00 1F 07 00 00 29 07 00 00 33 07 00 00
^^^^^^^^^^^ ^^^^^^^^^^^ ^^^^^^^^^^^ ^^^^^^^^^^^ ^^^^^^^^^^^
SP=0x20000400  Reset=0xA81  NMI=0x715   HardFault=0xA61 ...
~~~

`OMNI_RECEIVER_V06_00_25.bin`：

~~~text
98 38 00 20 A9 04 00 00 B1 04 00 00 B3 04 00 00 B5 04 00 00 ...
^^^^^^^^^^^ ^^^^^^^^^^^
SP=0x20003898  Reset=0x4A9
~~~

判定：**偏移 0 是 ARM Cortex-M 向量表**（字 0 = 初始栈顶 0x2000xxxx，即 SRAM 顶；字 1 = Reset 向量，低位为 1 表示 Thumb）。后续 15 个字全部落在 0x4xx–0x14xx 的低地址区间，符合 Cortex-M 的 16 个异常向量 + 外设中断表。
→ **固件是裸机扁平镜像**（直接烧 Flash 偏移 0），不是 Intel HEX / 自定义包头 / 压缩容器；升级工具通过 HID 中断端点把整个镜像流式写进 bootloader（`BootloaderPid=6863` 即 bootloader 的 PID）。

### 6.6 升级工具链角色

| 文件 | 版本 | 角色 | 依据 |
|---|---|---|---|
| `Firmware\Bin\FWUpdateSetup.exe` / `Firmware\Tools\FWUpdateSetup.exe` | 1.0.4.0 | **安装器**：复制固件/工具到 `ArmouryDevice\Firmware\*`，不参与刷写 | 字符串 `%s\Firmware\%s`、`Install %s success.` |
| `peripheral_fwu.exe` | 1.0.0.5 | 鼠标固件刷写主程序（CLI，`s 1ACE 1ACF 112 248 FF01 FF01 1` 这类命令驱动） | `6871\` 目录 + `version.ini` 的 `CMD` |
| `peripheral_fwu_pro.exe` | 1.0.0.7 | 接收器固件刷写主程序（同族增强版，`m 1ACE 1ACF 48 64 FF02 FF01 4`） | `6862\FW\` + `config.ini` 的 `Cmd 1` |
| `My_FW_Update_P520_Mouse.exe` | 2.0.0.2 | 鼠标更新入口壳（读 `version.ini`，调 `peripheral_fwu.exe`） | `6871\Dependence.ini` 的 `cmd=` |
| `FW_Update_Tool_Unify_Dongle.exe` | 4.0.0.1（产品名 `FW_Update_Tool_KB`） | 接收器更新入口壳（读 `config.ini`，调 `peripheral_fwu_pro.exe`） | `6862\Dependence.ini` 的 `cmd=` |
| `HidInterruptHandle.dll` | 1.0.4.0（`InterruptTransfer dll for FW update`） | HID 中断端点收发（写固件流） | PE 描述 |
| `InterruptTransfer.dll` | 6862: 1.0.111.11111 / 6871: 1.0.2.1 | 中断传输封装（另一套，两目录版本不同） | PE 版本 |
| `MsFWUpdate.dll` | 1.0.0.37 | **给 `asus_framework.exe` 用的 DLL**：框架进程内驱动升级，最终落 `ArmouryDevice\` 根 | `VersionList.txt` 的 `MsFWUpdate v1.0.0.37`；已装根目录存在该 DLL |
| `GetDeviceVersion.exe` | 1.0.0.4 | 查询设备当前固件版本，输出到同目录 `result.ini` | 已装 `ArmouryDevice\Firmware\Tools\{result.ini, ShowConfig.ini, Config.ini}` |
| `cpprest141_2_10.dll` | （无版本资源） | C++ REST SDK 2.10，HTTP 客户端（供 MsFWUpdate/工具联网） | 文件名 + 无版本信息 |
| `ROGWeChat.exe` | 1.0.0.2（WPF，`ROGWeChat.MainWindow`） | **与固件无关**的营销弹窗（"Join ROG WeChat group / Scan to register your products"），随工具包附带 | BAML 内嵌入的 XAML 原文 |

---

## 7. MultiLang（安装器 UI 语言包）

- 路径：`MultiLang\<Language>.ini`，**实测 23 个**文件，每个 1.3–2.2 KB。
- 结构：`[<Language>]` 段，`英文原键 = 译文`，共 15 条键；`{0}` 为产品名占位符。

`English.ini` 键集合（即 UI 契约）：

| 键 | 语义 |
|---|---|
| `{0} Installer` | 窗口标题 |
| `Install {0} app to set device configurations.` | 说明文案 |
| `Version: v` | 版本行前缀（拼接为 `Version: v{0}`） |
| `Installation Completed` / `Installation Failed` | 结果 |
| `Install` / `Cancel` / `Installing` / `OK` | 按钮/状态 |
| `Restart Now` / `Restart Later` | 重启选择 |
| `Some functions require a system reboot to complete setup.…` | 重启提示长句 |
| `Please restart the system and retry installation.…` | 失败提示 |
| `Launch {0} to start device setting.` | 完成页跳转 |

语言集合（23 种）：`Czech, Danish, Dutch, English, Finnish, French, German, Indonesian, Italian, Japanese, Korean, Norwegian, Polish, Portuguese, Romanian, Russian, Simplified Chinese, Spanish, Swedish, Thai, Traditional Chinese, Turkish, Ukrainian`。
漏译情况：`Japanese.ini` 的 `Launch {0} to start device setting.` 仍是英文原文，属未翻译残留。

---

## 8. Tools（VC 运行时）

| 文件 | 大小 | 版本 | 说明 |
|---|---|---|---|
| `Tools\vc_redist.x64.exe` | 25,424,536 B | 14.38.33130.0 | Microsoft Visual C++ 2015-2022 Redistributable (x64) |
| `Tools\vc_redist.x86.exe` | 13,846,856 B | 14.38.33130.0 | Microsoft Visual C++ 2015-2022 Redistributable (x86) |

- 参数（`Config.ini`）：`/quiet /norestart`。
- 两个都要装：`asus_framework.exe` 是 64 位 Electron，而 `SdkSetup.exe`/HttpSetup/HID 工具链是 32 位 MFC（`Device\p520\p520\Setup.exe` 内含 `mfc\viewcore.cpp` 路径、`AfxFrameOrView140su`）→ 需要 14.x 的 x64 与 x86 双运行时。
- 该包**不落到 ASUS 目录**，由微软安装器写系统级 `WinSxS`/注册表。

---

## 9. 安装顺序与安装链

`Config.ini` 的 `Item_1..Item_18` **就是执行顺序**（主安装器按序循环，`[AC Installer] Install file (` 每项打一条日志），并且每项先做 `VersionCompare`（本机已装版本 ≥ 包内版本则跳过）。

~~~text
ArmouryCrateGearInstall.exe  (1.0.3.0, 需管理员)
│  读 Config.ini → 读 MultiLang\<OS 语言>.ini → 检查 Armoury Crate Lite 是否已装
│
├─ 1  Tools\vc_redist.x64.exe            /quiet /norestart        → 系统 VC++ 2022 x64
├─ 2  Tools\vc_redist.x86.exe            /quiet /norestart        → 系统 VC++ 2022 x86
├─ 3  Framework\FrameworkServiceSetup.exe /install /quiet
│      └─(子进程) Framework\install\setup.exe  (InstallShield 3.0.0.4)
│         └─ 释放 FrameworkFileList.ini 的 19 文件 + 6 目录 → ArmouryDevice\
│         └─ 注册表 ArmouryDevice\{Installation location, Version=4.2.2.8, HTTPPort=1042}
│      └─ 回写 FrameworkServiceSetup.exe / config.ini / Process.ini / FrameworkFileList.ini
├─ 4  SDK_HAL\FWMSPlugin\Setup.exe        -s   → ArmouryDevice\modules\mouse\
├─ 5  SDK_HAL\MainSDK\SdkSetup.exe        -s   → ArmouryDevice\dll\   (ArmourySDK.dll 等)
├─ 6  SDK_HAL\MouseSDK\SdkSetup.exe       -s   → ArmouryDevice\dll\MouseSDK\
├─ 7  SDK_HAL\MouseHal\AacMouseSetup.exe  /quiet  → C:\Program Files\ASUS\Aac_Mouse\
├─ 8  Firmware\bin\FWUpdateSetup.exe      -s   → ArmouryDevice\Firmware\{6862,6871}\
├─ 9  Firmware\Tools\FWUpdateSetup.exe    -s   → ArmouryDevice\Firmware\Tools\
│         (+ MsFWUpdate.dll / cpprest141_2_10.dll 直接落 ArmouryDevice\ 根)
├─10  Device\p520\6871\Setup.exe          -s   → ArmouryDevice\View\6871\     ┐ 先装子页
├─11  Device\p520\6873\Setup.exe          -s   → ArmouryDevice\View\6873\     │
├─12  Device\p520\p520\Setup.exe          -s   → ArmouryDevice\View\p520\     ┘
├─13  Device\p520\Setup.exe               -s   → 设备级：WebAC\p520 + HomePage.exe + shortcut.lnk
├─14  Device\omni\omni\Setup.exe          -s   → ArmouryDevice\View\omni\
├─15  Device\omni\6862\Setup.exe          -s   → ArmouryDevice\View\6862\
├─16  Device\omni\Setup.exe               -s   → 设备级：WebAC\omni
├─17  SDK_HAL\UnifyDongleSDK\SdkSetup.exe -s   → ArmouryDevice\dll\UnifyDongleSDK\
└─18  SDK_HAL\unifyDonglePlugin\Setup.exe -s   → ArmouryDevice\modules\unifyDongle\
        ↓
   启动 ArmourySocketServer.exe（框架本地 socket 服务），显示"Installation Completed"
~~~

顺序推断的三条依据：

1. **依赖方向**：第 3 项 Framework 先于所有 SDK/插件（后者的注册表根就在 `ArmouryFramework`/`ArmouryDevice` 下，且 HttpSetup 的 `[Process]` 要停 `asus_framework.exe` —— 说明框架此时已存在）。
2. **页面父子倒序**：子页（10/11/12）先于父级（13）。父级 `Setup.ini` 的 `[Uninstall]` 要登记 6871/6873 的 GUID，且要生成 `HomePage.exe` 与 `shortcut.lnk`，必须在子页落地之后。
3. **卸载逆序由注册表锚点保证**：每个分包 `RLSSetting.ini` 的 `[Uninstall*]` 指向自己的 `HKLM\SOFTWARE\WOW6432Node\ASUS\{Reg}\{RegKey}\Uninstall`，父级 `Setup.ini` 的 `[Uninstall]count=N` 再显式列出子项 GUID；`FWUpdateSetup.exe` 的卸载是 `del %s\Firmware\*.*` 整目录清空。

**依赖声明文件 `Dependence.ini`**（仅固件分包有）把 `HTML/SDK/HAL/FrameworkMSPlugin` 全部置 0 —— 意味着**固件升级可以在完全没有 SDK/页面/框架的机器上独立运行**（这也是固件升级被拆成独立 setup 的原因）。

---

## 10. 安装包 → 安装目录 映射表

注册表根统一为 `HKLM\SOFTWARE\WOW6432Node\ASUS\`（主安装器以 `Item_Reg` + `Item_RegKey` 定位）；下表"注册表锚点"列为**实测存在**的键，括号内为实测版本值。

| # | 安装包路径 | 安装器（版本） | 安装到 | 注册表锚点（实测值） |
|---|---|---|---|---|
| 1 | `Tools\vc_redist.x64.exe` | MS VC++ 14.38.33130.0 | 系统（WinSxS / ARP） | — |
| 2 | `Tools\vc_redist.x86.exe` | 同上（x86） | 系统 | — |
| 3 | `Framework\FrameworkServiceSetup.exe` → `Framework\install\setup.exe` | InstallShield 3.0.0.4（包装器 3.0.0.3） | `C:\Program Files (x86)\ASUS\ArmouryDevice\` | `ArmouryDevice`（Version=4.2.2.8, Installation location, HTTPPort/HTTPSPort 1042/1043） |
| 4 | `SDK_HAL\FWMSPlugin\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\modules\mouse\` | `ArmouryFramework\mouse`（2.3.4） |
| 5 | `SDK_HAL\MainSDK\SdkSetup.exe` | InstallShield 2.0.0.3 / 1.00.0000 | `ArmouryDevice\dll\` | `ArmourySDK\MainSDK`（2.01.40） |
| 6 | `SDK_HAL\MouseSDK\SdkSetup.exe` | InstallShield 1.0.0.28 / 1.00.0000 | `ArmouryDevice\dll\MouseSDK\` | `ArmourySDK\MouseSDK`（3.01.48, `SdkPath=\MouseSDK\ArmouryMsSDK.dll`, SdkTypeId=1） |
| 7 | `SDK_HAL\MouseHal\AacMouseSetup.exe` → `Hal\AacMouseSetup.exe` | WiX Burn 1.2.1.18 | `C:\Program Files\ASUS\Aac_Mouse\`（2 DLL） | `Hal\{68AB32E8-…}` —— **实测不存在**（见 §11） |
| 8 | `Firmware\Bin\FWUpdateSetup.exe` | FWUpdateSetup 1.0.4.0 | `ArmouryDevice\Firmware\{6862,6871}\` | `WebAC\Firmware`（空键） |
| 9 | `Firmware\Tools\FWUpdateSetup.exe` | FWUpdateSetup 1.0.4.0 | `ArmouryDevice\Firmware\Tools\` + `ArmouryDevice\`（MsFWUpdate.dll、cpprest141_2_10.dll） | `WebAC\Firmware` |
| 10 | `Device\p520\6871\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\View\6871\` | `ArmouryHttp\6871`（4.05.38, PID=6871, FWVersion=4.00.02, FWDGVersion=7.00.08） |
| 11 | `Device\p520\6873\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\View\6873\` | `ArmouryHttp\6873`（4.05.38） |
| 12 | `Device\p520\p520\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\View\p520\` | `ArmouryHttp\p520`（4.05.38） |
| 13 | `Device\p520\Setup.exe` | WebAC Html Setup 1.0.3.1 | `ArmouryDevice\View\p520\` 追加 `HomePage.exe`/`Setup.ini`/`shortcut.lnk` | `WebAC\p520`（1.0.2.2） |
| 14 | `Device\omni\omni\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\View\omni\` | `ArmouryHttp\omni`（4.02.04） |
| 15 | `Device\omni\6862\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\View\6862\` | `ArmouryHttp\6862`（4.02.04） |
| 16 | `Device\omni\Setup.exe` | WebAC Html Setup 1.0.3.1 | `ArmouryDevice\View\omni\` 追加 | `WebAC\omni`（1.0.0.2） |
| 17 | `SDK_HAL\UnifyDongleSDK\SdkSetup.exe` | InstallShield 1.0.0.25 / 1.00.00 | `ArmouryDevice\dll\UnifyDongleSDK\` | `ArmourySDK\UnifyDongleSDK`（1.00.10, `SdkPath=\UnifyDongleSDK\AC_UnifyDongleSDK.dll`, SdkTypeId=21） |
| 18 | `SDK_HAL\unifyDonglePlugin\Setup.exe` | HttpSetup 1.0.2.9 | `ArmouryDevice\modules\unifyDongle\` | `ArmouryFramework\unifyDongle`（0.1.1） |

**卸载入口**：InstallShield 四包在 `C:\Program Files (x86)\InstallShield Installation Information\{GUID}\` 各留一份卸载体（实测存在 11 个 `{GUID}` 目录，对应 4 个 InstallShield 包 + 6 个设备页面 + 1 个 unifyDonglePlugin），GUID 与各 `Setup.ini`/`config.ini` 完全对应；`Aac_Mouse`（WiX）用 MSI 自己的 ARP 项。

---

## 11. 未验证项与不一致点

| 项 | 状态 | 说明 |
|---|---|---|
| `data1.cab` / `data2.cab` 内部文件清单 | **未验证** | InstallShield 私有 `ISc(` 容器；`expand -D` 只回显文件名不列条目；本机无 7z；`data1.hdr` 的 ASCII 扫描只得到 2 条噪声串（说明表被压缩/编码）。§5.2 的内容归属为体量反推（推断） |
| `MouseHal` 的 MSI 载荷明细 | **未验证** | WiX Burn `.wixburn` 内嵌 MSI，未解包；仅由已装结果确认最终落 `C:\Program Files\ASUS\Aac_Mouse\` 两个 DLL |
| `HKLM\SOFTWARE\WOW6432Node\ASUS\Hal\{68AB32E8-9C2B-4494-9921-E4E68F8B32A5}` | **实测不存在** | `Config.ini` 的 `Item_Reg7=Hal / Item_RegKey7={68AB32E8…}` 未兑现；MSI 可能只写自己的 ARP/ProductKey。是否需要该键才能被 AC 识别 → 未验证 |
| `WebAC\Firmware` vs `Config.ini` 的 `P520_Firmware` | **不一致** | `Config.ini` 声明 `Item_RegKey8=P520_Firmware`，实测只有 `WebAC\Firmware`（且无任何值）。推断 `FWUpdateSetup.exe` 改写了键名 |
| `FWMSPlugin` 版本 2.3.4 vs 主清单 2.4.3 | **不一致** | `SDK_HAL\FWMSPlugin\Setup.ini` 与注册表都是 2.3.4，而 `Config.ini`/`VersionList.txt` 写 2.4.3 |
| `FrameworkFileList.ini` 声明的 `node_modules`/`ssl`/`dll\WindowID` | **实测缺失** | `Test-Path` 全为 False，推断为完整版 Armoury Crate 才生成 |
| `MainSdkFileList.ini` 声明的 `FilterDriver`/`SwAgent`/`ShareFromArmouryIII`/`AcPowerNotification` | **实测缺失**（`ArmouryDevice\dll\` 下未见） | 同理推断属完整版组件 |
| 接收器固件 6.00.25 是否会被写入已装 7.00.08 的设备 | **未验证** | 需实机观察升级器版本比较逻辑（注册表 `FWDGVersion=7.00.08` > 包内 6.00.25） |
| `peripheral_fwu*.exe` 的 HID 协议细节（帧长/重传/CRC） | **未验证** | 本文只做静态结构分析；协议级细节归 HID 逆向章节 |
| `FrameworkServiceSetup.exe` 是否直接拉起 `asus_framework.exe` | **未验证** | 字符串只显示复制与执行 `install\Setup.exe`、`[ShellExecuteProcess]`；进程拉起更像由 `Process.ini` 的 `[Startup]` 与主安装器收尾的 `ArmourySocketServer.exe` 负责 |

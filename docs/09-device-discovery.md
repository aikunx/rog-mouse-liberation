# 09 · 设备发现链与 HID 调用面

> 补齐前八章缺失的一段：**软件如何“看见”设备**。这条链不走 SDK，走原生插件与 HID 枚举。
> 证据：ac_node_addon.node / UnifyScanner.dll / UnifyDeviceDetector.dll 的 PE 导出与字符串、AC_UnifyDongleSDK 的 HID 导入面。

## 9.1 三件套与职责

| 组件 | 位置（unpacked） | 大小 | 导出 | 职责 |
|---|---|---|---|---|
| ac_node_addon.node | ac_node_addon\build\Release\ | 463,704 B | napi_register_module_v1 等 2 个 | 暴露给主进程的 N-API 插件：设备枚举 + 系统事件 + 注册表 |
| UnifyScanner.dll | 同上 | 352,104 B | ScanUnifyDongle、ScanUnifyDongle_SDK、SetCallBackFunction、Release | HID 层扫描接收器 |
| UnifyDeviceDetector.dll | 同上 | 125,800 B | GetUnifyDeviceList、DetectedUnifyDgProcess、SetDeviceChangeCallBackFunction、SetUnifyScannerDllPath | 扫描结果转 XML 事件并回调 |

导入面：
- ac_node_addon：KERNEL32、USER32、ADVAPI32、SHELL32、ole32、DWrite、**WINTRUST**、CRYPT32、SHLWAPI、PROPSYS、**SETUPAPI**、**bthprops.cpl**、**POWRPROF**
- UnifyScanner：**HID.DLL**、SETUPAPI、SHLWAPI、SHELL32、OLEAUT32、ADVAPI32、KERNEL32
- UnifyDeviceDetector：KERNEL32、ADVAPI32、SHELL32、OLEAUT32、MSVCP140、VCRUNTIME140、VERSION

> 注意：ac_node_addon **不导入 HID.DLL**，它只做探测与系统集成；HID 枚举在 UnifyScanner。

## 9.2 ac_node_addon 暴露的 21 个 JS 方法（N-API 导出实证）

**设备枚举（三类）**
- getConnectUsbDeviceList
- getConnectUnifyDeviceList
- getConnectBtDeviceList

**插拔事件（注册/注销成对）**
- registerUsbChangedEvent / unregisterUsbChangedEvent
- registerBtChangedEvent / unregisterBtChangedEvent
- registerSystemEvent / unregisterSystemEvent
- registerHtmlUpdateEvent / unregisterHtmlUpdateEvent

**系统与注册表**
- getDWord / getString / setRegistry
- getDefaultUILanguage / getSystemFonts
- isAppExist / isWDLSupported / isWinVerifyTrust
- releaseMutex / startService

设备对象字段（字符串实证）：`vidpid`、`deviceSN`、`dongleSN`、`model_id`、`connectionType`、`bthAddress`、`connected`

注册表键：
- `SOFTWARE\WOW6432Node\ASUS\ArmouryDevice`
- `SOFTWARE\ASUS\ArmouryDevice`
- `SOFTWARE\ASUS\ARMOURY CRATE Diagnosis`

## 9.3 三类探测器的实现差异

### USB（UsbDeviceDetector）
- 创建 message-only 窗口接收设备通知（`[UsbDeviceDetector][Start] Failed to create message-only window`）
- `RegisterDeviceNotification` 注册接口变更
- 日志：`[UsbDeviceDetector][UsbChanged]`

### 蓝牙（BtDeviceDetector）
- `BluetoothFindFirstRadio` 找无线电
- 通过注册表把 bthAddress 反查 VID/PID：`GetVidPidFromBthAddress` → `RegOpenKeyExW` → `RegQueryValueExW VID|PID|Name`
- 事件：`DBT_DEVICEARRIVAL`、`DBT_DEVICEQUERYREMOVE`
- 日志：`[BtDeviceDetector] bthAddress: … connected: … connectionType: … model_id = …`

### 接收器（Unify 链路）
~~~
UnifyDeviceDetector.exe? 不，是同进程 DLL 链：
  UnifyDeviceDetector.dll
    -> SetUnifyScannerDllPath / SetDeviceChangeCallBackFunction
    -> UnifyScanner.ScanUnifyDongle (HID + SetupDi 枚举)
    -> 解析 XML: <device_type> / <device> <sn> / <device_changed>
    -> 回调上层（ac_node_addon）→ JS 事件
~~~
- 日志：`MainSDK\UnifyDeviceDetector`、`UnifyDeviceDetector.log`
- 关键错误串：`[ParseScanXml] Parse unify DG list xml <device_type> fail`、`device of this pid alrealy in list`、`[SentDeviceChangeEvent]`

## 9.4 HID 调用面（本机已装组件的实测边界）

> **2026-10 更新**：HID 帧格式已在 g-helper 源码中实证，见 **11 章**。本节关于「帧头无法证实」的结论仅针对官方组件。

AC_UnifyDongleSDK.dll 中存在的 HID API（字符串实证）：
- HidD_GetHidGuid
- HidD_GetAttributes
- HidD_GetPreparsedData / HidD_FreePreparsedData
- HidP_GetCaps / HidP_GetValueCaps / HidP_GetButtonCaps
- 写入侧无 HidD_Set*Report 字符串 → 走 KERNEL32 的 WriteFile

协议字段（同一模块字符串）：`device_type`、`device`、`function`、`devicePage`、`deviceType`、`deviceSN`、`command`、`data_name`、`device_changed`、`Payload Bytes: `

### 关于 `51 31` 帧头的结论（重要修正）

在 AC_UnifyDongleSDK 全文件 1,243,772 字节中搜索相邻字节 `51 31`：
- .text 段命中 **1 处**（0x100313FD，函数 FUN_10031000），反编译确认是**十六进制字符串解码逻辑**（处理 `0x` 前缀、逐字符转 byte），与帧构造无关
- 其余命中全部在 .reloc（重定位数据，噪声）

因此：
1. `51 31` 帧头**无法在本机已安装的组件中证实**
2. 该帧格式最可能属于**未安装的 MouseHal 分包**（安装包 `SDK_HAL\MouseHal`，对应 `AacMouseHal_x86.dll`），本机实际 HAL 为 AC_UnifyDongleSDK
3. 本机 HID 写入的真实形态是「HidD 属性查询 + WriteFile 原始报告」，报告内容由上层数据驱动，而非固定帧头

> 原始逆向记录里的 `51 31 <子命令> 00` 应标注为「来自 MouseHal 分包，本机未验证」。

## 9.5 主进程调用 DLL 的真实机制（已由 07 章源码级证据修正）

**先前推断（错误）**：主进程用 koffi 直接 FFI 调用 ArmourySDK.dll。
**实测（07 章，基于 source map 还原的 TS 源）**：
- SDK 加载走 `ac_node_addon` 暴露的 `addon.Library`，不是 koffi
- 调用形态：`Post_EntrypointReturn(xml, cb(err, status, data))`，异步回调
- 加载前用 `acAddon.isWinVerifyTrust` 做签名校验
- koffi 仍在 unpacked 依赖里（win32_x64 2.34 MB 等），但**不是 SDK 调用路径**——推断为其他插件或历史遗留

这条修正的意义：SDK 调用被封装在原生插件内部，因此 app.asar 里搜不到 `ExecuteFunction` 字面量，也解释了「独立进程直调不可靠」——必须借 ac_node_addon 的初始化上下文。

## 9.6 与既有记录的差异

| 既有记录 | 实测 |
|---|---|
| 设备枚举通过 SDK（function key 10） | 枚举主路径是 ac_node_addon 的三类探测器；SDK 的 key=10 是**协议层**枚举，不是发现层 |
| HID 帧头固定 51 31 | 本机组件无此常量；属于未安装的 MouseHal |
| 主进程通过 koffi 绑 SDK | 走 ac_node_addon.Library + Post_EntrypointReturn，加载前验签 |

## 9.7 未验证

1. UnifyScanner 的 HID 报告长度与子命令（需动态捕获或对 UnifyScanner 完整反编译）
2. ac_node_addon 里 startService / releaseMutex 的具体服务名与互斥体名
3. `model_id` 与 PID 的映射表来源（注册表还是内置表）
4. MouseHal 分包 MSI 内的 AacMouseHal_x86.dll 是否含 51 31 帧（安装包未解）

# 10 · 动态分析与解包（本轮新增）

> 本节记录**运行时**证据与安装包解包结果。静态分析到此为止的部分，用动态方法补齐。

## 10.1 环境与工具链（已就绪）

| 项 | 状态 | 说明 |
|---|---|---|
| Frida | 17.24.1（pip 安装成功） | 用户态注入，可 hook HID.DLL、KERNEL32、SDK 导出 |
| olefile | 已装 | 解析 MSI 的 OLE 复合文档 |
| msilib / expand.exe | 可用 | 解 CAB |
| 目标进程 | asus_framework.exe（ia32） | 加载 ArmouryMsSDK / AC_UnifyDongleSDK / AacMouseHal / ac_node_addon |
| 官方后台 | ArmourySocketServer.exe | 需**管理员**权限启动（WinError 740） |

已确认的进程模块分布（动态枚举）：

| PID | 加载的硬件相关模块 |
|---|---|
| 4008 | ArmouryMsSDK.dll、ArmourySDK.dll、AC_UnifyDongleSDK.dll、ac_node_addon.node、UnifyScanner.dll、UnifyDeviceDetector.dll |
| 13064 | ArmouryMsSDK.dll、**AacMouseHal_x86.dll**、ac_node_addon.node、UnifyScanner.dll、UnifyDeviceDetector.dll |

> **重要修正**：`AacMouseHal_x86.dll` 的真实路径是 `C:\Program Files\ASUS\Aac_Mouse\AacMouseHal_x86.dll`（64 位程序目录，不在 `Program Files (x86)\ASUS` 下）。02 章「不存在」的结论据此更正为「存在但不在该目录」。

## 10.2 安装包解包结果（MouseHal 分包）

`SDK_HAL\MouseHal\Hal\AacMouseSetup.exe`（4,599,160 B）→ 扫描出 2 个 CAB（偏移 0x74E00 / 0x960D8）→ `expand.exe` 解出 10 个文件，其中：

| 文件 | 大小 | 格式 | 备注 |
|---|---|---|---|
| a0 | 1,527,808 | OLE 复合文档（MSI） | 内含 CAB stream（1,477,430 B） |
| a1 | 2,347,008 | OLE 复合文档（MSI） | 内含 CAB stream（2,293,761 B） |
| a2 / a3 | 344,032 | PE32 x86 | 签名引导器（无导出） |
| u0 | 210,200 | PE32 x86 | WiX Burn bootstrapper |
| u1 / u2 / u4 / 0 | 3–8 KB | XML | 安装配置 |
| u3 | 2,962 | PNG | 图标 |

从两个 MSI 的 CAB 中解出真正的 HAL：

| 文件 | 大小 |
|---|---|
| **AacMouseHal_x86.dll** | 3,361,120 B |
| **AacMouseHal_x64.dll** | 2,849,120 B |
| AsusInstallVerifier.exe / _x64.exe | 828,136 B |
| mousehasher.hash / _x64.hash | 82 B |

### AacMouseHal_x86.dll 的结构

- 5 个导出，全部是 COM 标准导出：`DllGetClassObject`、`DllCanUnloadNow`、`DllRegisterServer`、`DllUninstallServer`、`DllInstall`
- 导入：SETUPAPI、SHLWAPI、WS2_32、**BluetoothApis**、SHELL32、OLEAUT32、ADVAPI32、ole32、USER32、**HID.DLL**、KERNEL32
- 即：它是 **COM 服务 + HID/蓝牙双通道**，能力通过 COM 接口暴露，不是平面导出函数

### `51 31` 帧头的最终判定（官方组件内证伪，第三方实现中证实）

> **更新**：g-helper（第三方）用 `03 51 31 <子命令> 00` 与 P520 通信，见 11 章。官方组件不发 HID，故二进制中无常量。

| 位置 | `51 31` 命中 | 判定 |
|---|---|---|
| AC_UnifyDongleSDK.dll（1,243,772 B） | .text 1 处（0x100313FD） | 反编译为十六进制字符串解码，非帧构造 |
| AacMouseHal_x86.dll（3,361,120 B） | 25 处 | 6 处在文件头；2 处是 `call rel32` 指令字节；其余是 **RVA 指针值**（`0x10315130` 的中间字节）与 **UTF-16 字符区间表** |
| ArmouryMsSDK / SocketServer | — | 无 |

**结论：`51 31 <子命令> 00` 不是固定帧头。** 本机所有已安装与安装包内的组件都没有把它当作常量使用。真实 HID 报告格式必须通过运行时捕获确定。

## 10.3 运行时捕获：已打通的链路

### 捕获 1：HTTP 请求确实进入 SDK，并暴露了必填字段

发送 `PUT /type/1/model/6871/performance` 后，SDK 日志出现完整调用链：

~~~
{in} ExecuteFunction
+++++strInputXml+++++
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<root><device_type key="1"><device key="6871"><function key="1">
  <profile_index>1</profile_index>
  <dpi_number>4</dpi_number>
  <accelerate>-1</accelerate> ... <polling_rate>-1</polling_rate>
  <color><dpi_1>-1</dpi_1>...</color>
  <dpi_1/><dpi_2/><dpi_3/><dpi_4/>
</function></device></device_type></root>
------strInputXml-----
[ArmouryMsSDK]enter function 1
[ParsePerformanceXml]no high performance mode
[ParsePerformanceXml]no dpi channel
[ParsePerformanceXml]no dpi onoff
[ParsePerformanceXml]no color
[ParsePerformanceXml]no dial mutipller
[ParsePerformanceXml]no dial sensitivity
[ParsePerformanceXml]error index: 5
function 1 fail
~~~

**结论**：`SetPerformance` 需要 6 个额外字段（high_performance_mode、dpi_channel、dpi_onoff、color、dial_mutipller、dial_sensitivity），缺一即失败。这是官方页面之外从未走过的路径——历史日志中`high_performance_mode`出现次数为 0。

### 捕获 2：设备存活与电量轮询（真实周期事件）

~~~
[ImmediActionEvent]PID:6871 is alive / isn't alive
[GetPowerStatus_AutoResponseEvent]exit GetPowerStatus_AutoResponseEvent 42
~~~

- 每 60 秒一次电量回读，返回值 42（电量百分比）
- 设备掉线/上线通过 ImmediActionEvent 事件通知
- 这解释了页面电量显示的刷新节奏

### 捕获 3：`modules/mouse/index.js` 的 setPerformance 实现（源码级）

```js
// 关键映射（camelCase → XML 蛇形）
const F = C.root.device_type.device.function;
F.profile_index = profileID;
F.dpi_number = dpiNumber || caps.performance.dpiAmount;
F.accelerate = settings.accelerate || "-1";
F.polling_rate = settings.pollingRate || "-1";
if (settings.pollingRateWireless) F.polling_rate_wireless = ...;
if (settings.currentDPI)  F.dpi_channel = settings.currentDPI;   // ← 注意：currentDPI 映射到 dpi_channel
if (settings.dpiOnoff)    F.dpi_onoff = settings.dpiOnoff;
if (settings.dialMultiplier) F.dial_mutipller = settings.dialMultiplier;
if (settings.dialPopUpDelay) F.dial_sensitivity = settings.dialPopUpDelay;
for (R = 1; R <= caps.performance.dpiAmount; R++)
  F["dpi_" + R] = settings["dpi" + R + "XY"] || settings["dpi" + R];
```

**关键**：`dpi_amount` 来自 **caps.performance.dpiAmount**，不是请求体；`color` 需要显式传入才生成。

### 捕获 4：HTTP 路由的真实边界

主进程 `lib.ts` 的 `Router.routingResource` 只认：

~~~
regularResourcesList = ['file', 'image', 'collect']
simpleResourceList  = ['devices','fonts','language','view','window','install','app','process']
~~~

**`performance` 不在其中**。所以 `PUT /type/1/model/6871/performance` 落到 default 分支返回 500；页面真正改设置的路径是 **WS（role=deviceService）→ 主进程 dispatch**，不是 HTTP PUT。

> 08 章「页面业务走 HTTP PUT」的判断需要修正：HTTP 只承载 file/image/collect 与 8 个简单资源；设备设置走 WS。

## 10.4 尚未捕获的部分（阻塞点明确）

| 缺口 | 阻塞原因 | 解除条件 |
|---|---|---|
| 真实 HID 报告字节 | 需官方进程在执行一次成功设置；当前 `ArmourySocketServer.exe` 需**管理员**启动（WinError 740），已运行的 `asus_framework`（16680）拒绝 frida 注入 | 以管理员身份启动官方后台，或手动打开 Armoury Crate 操作一次 |
| 成功的 SetPerformance XML | 同上 | 同上 |
| WS 消息体 | 需 hook 渲染进程或自建 WS 客户端（role=deviceService） | 可后续用 frida 连渲染进程实现 |

## 10.5 可复现的抓包脚本（已备好）

工具在 `rog-reverse/tools/`：

| 脚本 | 作用 |
|---|---|
| `hook_both.js` + `hook_both.py` | 同时挂 `ArmouryMsSDK.ExecuteFunction` 与 4 个 `HidD_*` 写读函数，记录 XML 与报告字节 |
| `hook_ef.js` + `hook_ef.py` | 只挂 ExecuteFunction，观察所有 XML |
| `hook_run.py` | 只挂 HID 写函数 |
| `probe_hid.py` | 枚举进程模块与 HID.DLL 导出 |

用法（进程稳定时）：

~~~
python tools/hook_both.py data/capture.jsonl 120   # 抓 120 秒
~~~

然后在 Armoury Crate 里改一次 DPI 或灯效，抓包文件里就会出现真实的 XML 与 HID 报告。

## 10.5.1 注入失败的实测记录

对唯一存活的 `asus_framework.exe`（PID 16680）连续两次 `frida.attach` 均失败：

~~~
attach fail 16680 process with pid 16680 either refused to load frida-agent, or terminated during injection
~~~

而此前 PID 4008 / 13064 注入成功并能正常 hook（10.3 的证据即来自它们）。差异说明：**同一程序的不同实例注入结果不同**，当前实例要么带反注入状态，要么处于异常态。同时 `ArmourySocketServer.exe` 需要管理员权限才能拉起（WinError 740）。

因此动态抓包的完整闭环需要：以管理员身份启动官方后台 → 等进程稳定 → 运行 `tools/hook_both.py` → 在 UI 里改一次设置。

## 10.6 本轮结论

1. **解包完成**：MouseHal 的 MSI/CAB 全部解开，拿到官方 HAL 的 32/64 位 DLL。
2. **`51 31` 帧头证伪**：三处组件、25 处命中，全部是巧合（指令字节、RVA、UTF-16 表）。
3. **动态链路打通**：证明 HTTP 请求能进 SDK、能触发 `SetPerformance`、能看到完整失败原因与必填字段；电量轮询与掉线事件的时间语义已确认。
4. **仍未拿到 HID 报告字节**：需要一次「官方软件以管理员运行 + 一次真实设置操作」，当前被权限与注入拒绝挡住。
5. **路由模型修正**：设备设置走 WS，不是 HTTP PUT。

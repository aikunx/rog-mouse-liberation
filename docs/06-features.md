# 06 · 功能与代码实现对照

> 回答两个问题：这个软件**能做什么**，每个能力在**代码里落在哪**。
> 读者可以是人，也可以是 AI：所有行都能顺着证据文件继续下钻。

## 6.1 能力全景（来自 6871 页面的 caps.json）

~~~~json
{
  "device": "ROG STRIX IMPACT III WIRELESS (2.4GHz)",
  "productName": "P520", "deviceType": "1",
  "multiModelNumber": ["6871", "6873"],
  "profileCount": 5,
  "profiles": { "hasRapidFireProfile": true },
  "deviceProps": { "isWireless": true, "isAuraSyncEditable": true,
                   "isSupportMacroShift": true, "isSupportCustomKeyCode": true },
  "functionList": ["Buttons", "Performance", "Lighting", "Calibration", "Power",
                   "Promotion", "FirmwareUpdate"]
}
~~~~

7 个功能页 = 软件对外承诺的 7 类能力；5 个 Profile；支持 RapidFire、AuraSync、宏 Shift、自定义按键码。

## 6.2 功能 → 实现矩阵（核心表）

链路四段固定：**页面 UI → 主进程 → JSON 转 XML → SDK function key → 设备**。

但**写操作与查询走不同通道**（08 章实证）：
- 写：页面直接 `HTTP PUT /performance | /buttons | /lighting | /calibration | /power | /dial`，体为 `{profileID, data, isPreview}`
- 查：10 条查询/回显命令走 XML（如 GetDeviceProfileList、GetDeviceDescription）
- 事件：WS（`broadcastEvent`、`socketServerEvent`、`DevicePlugInNotify`）由服务端推送

下表的「服务层命令」列同时给出写端点与查询命令。

| 功能 | 页面入口 | 写入端点 / 查询命令 | XML function key | SDK 内部函数 | 设备侧 |
|---|---|---|---|---|---|
| 按键/宏 | Buttons | PUT /buttons ; GetDeviceProfile | 0 SetKeyChange | FUN_1003b1f0 | 键映射写设备 |
| 性能（DPI/轮询） | Performance | PUT /performance | 1 SetPerformance | FUN_1003ce40 → FUN_10017960 | DPI 档 / 轮询率 |
| 灯效 | Lighting | PUT /lighting ; GetDeviceLightingControl | 2 SetEffect | FUN_1003a330 | RGB / 灯效引擎 |
| 校准 | Calibration | PUT /calibration | 3 SetCalibration | FUN_10039830 | 表面校准（材质/高度） |
| 省电 | Power | PUT /power | 4 SetPower | FUN_1003d610 | 休眠/低功耗 |
| 配置切换 | 顶栏 Profile | SetDeviceProfile / DeviceProfileChange | 5 SetProfile | FUN_1003d890 | 5 组 profile |
| 读设备数据 | 各页 | GetDeviceDescription | 6 SetLoadData | FUN_1003c7b0 | 电量/固件/rf |
| 灯效同步 | AuraSync | GetAuraSyncState / linkAuraSync | 7 SetSync | FUN_1003e4b0 | Aura 总线 |
| 帧动画 | Lighting | broadcastEvent | 8 SetFrame | FUN_1003ac60 | 逐帧下发 |
| 按键记录 | 调试 | writeLog | 9 SetKeyLog | FUN_1003bc10 | keylog 开关/事件 |
| 枚举设备 | 启动 | IsNewDevice / DevicePlugInNotify | 10 Enumerate | FUN_10014ab0 | 设备表 |
| 写文件 | Promotion/资源 | exportFile / getFileDetail | 11 SetWriteFile | FUN_1003e890 | 设备内文件 |
| 删文件 | 同上 | — | 12 DeleteFile | FUN_10039ca0 | 清设备文件 |
| 通知/事件 | 全局 | broadcastEvent / ShowToast | 13 SendNotification | FUN_10039170 | 事件回推 |
| （保留） | — | — | 14 | FUN_10038790 | 未定位 |
| 拨盘 | Buttons | PUT /dial | 15 SetDail | FUN_10039b20 | dial 灵敏度/倍率 |
| 摇杆模式 | Buttons | rePluginDevice | 16 SetJoystickMode | FUN_1003b060 | 体感/摇杆 |
| 固件升级 | FirmwareUpdate | checkFWUpdate(主进程 MsFWUpdate.dll) | 17 SetFwUpdate | FUN_1003ae20 | bootloader |
| 设备重置 | 设置 | rePluginDevice | 18 SetDeviceReset | FUN_1003a1b0 | 恢复出厂 |
| 演示模式 | 展示 | switchApp | 19 SetDemoMode | FUN_1003a020 | 灯效演示 |
| WDL | Lighting | syncWDLControlStatus | 20 SetWDL | FUN_1003e700 | Windows 动态光 |

> 上表 function key 与内部函数地址来自 Ghidra 对 ExecuteFunction 的反编译（04 章 §4.4），非推测。

## 6.3 关键实现机制

### A. JSON → XML 的唯一转换点
设备模块（modules/mouse、modules/unifyDongle）把页面 JSON 经 `parseJSON2XML` 转成 XML，再 `send({method:"post", deviceType, sessionKey, xml})` 交给主进程。**页面永远不写 XML**，XML 只存在于「模块 → SDK」这一段。

### B. sessionKey / deviceType 的会话语义
- `sessionKey` 标识一次页面会话，贯穿 HTTP 与 WS
- `deviceType` 区分设备类别（鼠标为 1；框架另有 21 等），决定模块路由

### C. caps / profile 的数据驱动
- `caps.json` 定义功能开关与设备属性，页面据此渲染
- `profileCount: 5` → 5 组配置，切换走 key=5
- 出厂默认值在 `_ref/default.json`（仅 6871 存在）

### D. 错误码分层
| 段 | 类别 |
|---|---|
| -1 | UNKNOWN |
| 1000–1006 | SDK/设备/进程类（未找到、崩溃、重建、失败、不在线、配置缺失） |
| 1010–1015+ | 资源/文件/插件类 |
| 4001–4005 | 状态类（ICAFE、AuraSync、存活、bootloader、webapp） |
| 5001–5003 | Aura/快捷键/应用存在性 |
| 20000+ | WebSocket 目标/电源/托盘 |
| S-1000…S-1008 | 主进程侧同名错误的跨进程透传 |

### E. 两套事件总线
- 主总线 1042（HTTP+WS）
- 提示/日志旁路：`ws://127.0.0.1:9013 alertSocketServer`、`ws://127.0.0.1:9014 logSocketServer`
- WS role：`home | devicePage | deviceService | devicePageLogger | socketServer`

### F. 服务进程生命周期
页面侧 `service.js` 由主进程以 `utilityProcess.fork` 拉起，崩溃后对应错误码 1001/1002（PROCESS_CRASHED / PROCESS_REBUILD）。

## 6.3.1 会话与并发的两个硬约束（08 章实证）

- **sessionKey 回包按 cmd 匹配**：同一命令不可并发，否则回包错配
- **DPI 存档位索引**：`index = ceil((v - 100) / 50) + 1`（与 04 章的整数除公式在非整步进时不同，以前端为准）

## 6.4 各功能实现要点

**性能（DPI/轮询）**
- 字段：dpi_number、dpi_1..dpi_4、polling_rate、polling_rate_wireless、angle_snapping、angle_tuning、button_response、high_performance_mode
- DPI 编码（既有记录）：index = (dpi-100)/50 + 1；范围 100–36000，步进 50
- 轮询率：0..3 = 125/250/500/1000 Hz
- SDK 侧按 PID 分支（伪代码出现 `param_1 == 0x1824`）

**灯效**
- 字段：effect_id、color_number/color_r/g/b、brightness_wireless、frame_effect/frame_count、sync_*
- effect_id：0 静态、1 呼吸、2 循环、4 反应、6 电量、240 关、241 AuraSync
- AuraSync 走 linkAuraSync/unlinkAuraSync + GetAuraSyncState，与 Armoury Crate 的 Aura 服务联动

**按键/宏**
- 字段：button_number、button_function、key_change、key_0..N、source_key、target_key_1..4、windows_shortcut、image_dir、cursor_position
- 键位定义表：`_ref/keyDefinitions/<locale>.json`（20+ 语言，含 _mac 变体）
- 初始化文件命名：`button%d.xml`（按型号分文件）

**校准**
- 字段：calibration_function/material/height/result；结果回写 calibration_result

**配置（Profile）**
- profile_index / profile_number / profile_function / profile_parameter
- 落盘：ProgramData\ASUS\ArmourySDK\MouseSDK\P520\performance1.xml 等

**固件升级**
- 主进程 MsFWUpdate.dll：CheckFWUpdate → QueryDevice → UpdateDevices（cpprest HTTP 下载 + HID 烧写）
- 工具：peripheral_fwu.exe（鼠标）、peripheral_fwu_pro.exe（接收器）
- 状态目录：ProgramData\ASUS\ARMOURY CRATE Diagnosis\MAFWUpdate\<产品名>\

**读设备数据**
- 电量、rf_version、usb_version、stick_mode、booster_version 全部经 key=6 回包
- 实现是 data_number 二级分派（约 25 个读取分支，见 04 章 key=6 骨架），不是一条固定命令

## 6.5 给人类的阅读路径

1. 先看 00 索引拿全貌
2. 想改行为 → 05（架构）+ 06（功能矩阵）
3. 想追协议 → 04 §4.4/§4.5（key 表与字段全集）
4. 想复现安装 → 01；想看装了啥 → 02
5. 想改页面 → 03

## 6.6 给 AI 的读取约定

- `rog-reverse/data/` 是原始证据，可直接 grep：
  - `pe-analysis.json`：8 个二进制的导出/导入
  - `str-*-a8.txt` / `str-*-u16.txt`：4 个模块的字符串全集
  - `pseudo-ExecuteFunction.txt`、`pseudo-SetPerformance.txt`：Ghidra 伪代码
  - `asar-paths.txt`：app.asar 内 1,101 条路径
  - `ms-sdk-overview.json`、`asar-js-analysis.json`：分析元数据
- 文档中的「推断/未验证」标签即不确定性边界，不要当成事实使用

## 6.7 未验证

1. function key 14 的业务语义
2. 设备端 HID 子命令字节与 key 的对应表（需动态捕获）
3. `deviceType` 全部取值表（当前确认 1、21）
4. 9013/9014 旁路总线的完整消息格式
5. Promotion（推广位）功能的后端数据源

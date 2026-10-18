# 00 · ROG STRIX IMPACT III WIRELESS 软件逆向总索引

**目标**：把 ArmouryDevice 生态（安装包 + 已安装 + 页面 + 二进制 + 运行时数据）完整结构化。人与 AI 都能顺着证据下钻。

## 一句话架构

页面（React）→ 主进程 express/WS(1042) → deviceService 子进程与设备模块 → JSON 转 XML → ArmouryMsSDK.ExecuteFunction → HID → Omni 接收器 → 鼠标。

设备"被看见"走另一条链：ac_node_addon（USB/蓝牙/Unify 探测器）→ 通知主进程 → 页面。

## 文档地图

| 文件 | 内容 | 体量 |
|---|---|---|
| 00-index.md | 本文件：全貌、速查、纠错、证据索引 | 7 KB |
| 01-installer.md | 安装包：六个分包、Framework、固件格式、安装链、映射表 | 43 KB |
| 02-installed.md | 已装目录：逐文件 PE 版本、45 型号、asar 106 包、ProgramData、注册表、拓扑 | 67 KB |
| 03-pages.md | 页面层：四页面、bundle、service.js、caps、按键定义、语言包 | 70 KB |
| 04-binaries.md | 符号级：导出/导入矩阵、21 个 function key 分派、XML 字段全集、命令面 | 14 KB |
| 05-architecture.md | 分层、端口、三套协议、三条数据流、启动链 | 7 KB |
| 06-features.md | 功能 → 代码实现矩阵、通道模型、各功能要点 | 9 KB |
| 07-main-process.md | 主进程与 service.js：source map 还原 22 个 TS 源、WS/路由、addon.Library 调 SDK | 36 KB |
| 08-pages-code.md | 页面业务：7 个功能页组件、HTTP PUT 写通道、XML 查询、会话约束 | 36 KB |
| 09-device-discovery.md | 设备发现链（USB/BT/Unify）、ac_node_addon 的 21 个 API、HID 调用面 | 7 KB |
| 12-keys-and-macro.md | 按键重映射协议（实测）、宏的逆向结论（设备不存宏） | 5 KB |
| 11-asus-hid-protocol.md | **ROG 鼠标 HID 协议**（g-helper 实证）：P520 匹配、完整命令表、电量/DPI/灯效解析 | 9 KB |
| 10-dynamic-analysis.md | 动态抓包与解包：MouseHal MSI 解开、51 31 证伪、真实 XML 与失败链、抓包脚本 | 8 KB |

## 组件速查

| 层 | 组件 | 角色 |
|---|---|---|
| UI | View\{p520,omni,6871,6862} | React 页面，按 PID 路由 |
| 服务 | service.js（utilityProcess） | 页面侧服务，反向连主进程 role=deviceService |
| 主进程 | asus_framework.exe（Electron 29.4.6） | express + WS、设备模块、addon.Library |
| 总线 | ArmourySocketServer.exe | WebSocket++ 0.8.2，单实例，看护主进程 |
| 原生 | ac_node_addon.node | N-API：设备枚举 + 系统事件 + 注册表 + 加载 SDK |
| 探测 | UnifyScanner.dll / UnifyDeviceDetector.dll | HID 枚举接收器，XML 事件回调 |
| SDK | ArmouryMsSDK.dll | XML 入口，21 个 function key |
| SDK | AC_UnifyDongleSDK.dll | HID 直连 + WS 客户端 |
| 辅助 | InstallHelper / MsFWUpdate / AsTaskSched / HidInterruptHandle | 安装、固件、计划任务、HID |

## 关键字号（写脚本直接抄）

- 页面：`http://127.0.0.1:1042/<PID>/index.html?isElectron=1&session=<uuid>`
- 端口：取自注册表 `HKLM\SOFTWARE\WOW6432Node\ASUS\ArmouryDevice\HTTPPort`（默认 1042），另 `WebSocketTarget`
- 写操作：`PUT /performance | /buttons | /lighting | /calibration | /power | /dial`，体 `{profileID, data, isPreview}`
- WS role：home / devicePage / deviceService / devicePageLogger / socketServer
- 旁路 WS：`ws://127.0.0.1:9013` alert、`9014` log（归 SocketServer）
- XML：`<root><device_type key="1"><device key="{PID}" sn="{SN}"><function key="N">…</function></device></device_type></root>`
- SDK 调用：`ac_node_addon.Library` → `Post_EntrypointReturn(xml, cb(err, status, data))`，加载前 `isWinVerifyTrust`
- 互斥体：`Global\ArmourySocketServerMutex`
- 计划任务：`\ASUS\ArmourySocketServer`、`\ASUS\AcPowerNotification`、`\ASUS\Framework Service`
- 配置：`dll\config.ini`（13 条 Type ID → DLL）、`View\6871\config.ini`（DeviceType=1、DG=6871、BT=6873）

## 实现层的硬事实

1. **写用 HTTP，查用 XML**：页面业务是 HTTP PUT JSON，只有 10 条查询/回显命令走 XML。
2. **JSON→XML 唯一转换点**：设备模块的 `parseJSON2XML`，页面永不写 XML。
3. **SDK 在原生插件内被调用**：所以 app.asar 搜不到 `ExecuteFunction` 字面量。
4. **source map 完整**：app.asar 的 `.webpack/main/index.js.map` 含 `sourcesContent` 466 项，可还原 22 个原始 TS 文件。
5. **sessionKey 回包按 cmd 匹配**：同一命令不可并发。
6. **DPI 存档位索引**：`ceil((v-100)/50)+1`（前端为准）。
7. **key=6 是二级分派**：data_number 0..0x18 共 25 项，不是一条固定命令。
8. **key=1 的主函数是设备上下文获取**（`FUN_10017960`），不是性能业务本身。

## 与原始逆向记录的差异（实测纠错）

| 原记录 | 实测 |
|---|---|
| AacMouseHal_x86.dll 实现 HID | 已装目录无此文件；HID 在 AC_UnifyDongleSDK / HidInterruptHandle / InterruptTransfer |
| HID 帧头固定 `51 31` | 本机组件无此常量（.text 唯一命中是十六进制解码逻辑）；最可能属于未安装的 MouseHal 分包 |
| ArmouryHtmlDebugServer.exe 页面宿主 | 不存在；页面由主进程 express 的 /view/ 提供 |
| View\6873 蓝牙页 | 已装目录无此目录；6873 只是 PID（BT） |
| P520 有 MouseSDK 型号目录 | 无；配置在 ProgramData |
| 页面直调 SDK | app.asar 里 ExecuteFunction 出现 0 次 |
| modules\mouse 是 ac_node_addon 桥 | 实为框架 checkModules() 挂的 Express 设备模块 |
| 主进程用 koffi 调 SDK | 走 ac_node_addon.Library + 验签；koffi 仍在依赖里但不在该路径 |
| 端口硬编码 1042/1043 | 读注册表 HTTPPort |

## 证据索引（rog-reverse/data/）

| 文件 | 内容 |
|---|---|
| pe-analysis.json | 8 个二进制导出/导入/节表（54 KB） |
| str-ms-sdk-a8.txt / -u16.txt | ArmouryMsSDK 字符串 |
| str-socket-server-a8.txt | SocketServer 字符串（3,661 条） |
| str-unify-dongle-a8.txt | 接收器 SDK 字符串（5,226 条） |
| str-armoury-sdk-a8.txt | 框架 SDK 字符串 |
| asar-paths.txt | app.asar 内 1,101 条路径 |
| pseudo-HID-frame.txt | 51 31 命中函数的伪代码（证伪用） |
| pseudo-SetLoadData.txt | key=6 二级分派 |
| pseudo-socketserver-role.txt | 端口读取与看护逻辑 |

> ExecuteFunction 分派表、SetPerformance 调用链已内联在 04 章。

## 工具链状态（复现必读）

- 可用：Ghidra 12.1.4（PE 原生）、.NET 静态、产物图/ASAR、JavaScript 静态、PE 资源
- 不可用：Hopper（macOS）、IDA/JADX（未配置）、objdump/rizin/tshark/frida（未装且外网不可达）
- 坑 1：REA `binary_session` 看不到 ghidra 多半是当前 target 为归档；对 PE 用 `open_binary(provider_id:"ghidra")`
- 坑 2：pwsh 沙箱内 Node child_process 一律 EPERM，`rea doctor` 的 java 检查是假阴性
- 坑 3：读 .asar 必须用 `original-fs`，否则 Electron 把 asar 当目录

## 阅读路径

- 想改行为：05 → 06 → 07/08
- 想追协议：04 §4.4/§4.5 → 09 §9.4
- 想复现安装：01 → 02
- 想改页面：03 → 08
- 只想看结构：02 → 03

## 未验证汇总（全局）

1. 1043 端口语义、8080 是否 listen
2. 计划任务 `\ASUS\Framework Service` 内容（权限拒绝）
3. SocketServer 与 framework 的实时父子 PID
4. HID 报告字节格式（本机组件未证实；属 MouseHal 分包）
5. function key 14 语义、deviceType 全取值
6. data1.cab / data2.cab 内部清单（InstallShield 私有）
7. 接收器固件 6.00.25 与已装 7.00.08 的取舍
8. UnifyScanner 的 HID 报告长度与子命令

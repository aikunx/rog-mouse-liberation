# 04 · 二进制组件与符号级结构

> 本章数据全部来自 REA（Ghidra 12.1.4 provider + PE 资源解析 + 字符串/伪代码）+ 自写 PE 导出/导入解析器。
> 证据文件：\`rog-reverse/data/pe-analysis.json\`、\`pseudo-ExecuteFunction.txt\`、\`pseudo-SetPerformance.txt\`、\`str-*-a8.txt\`。

## 4.1 组件总表

| 组件 | 路径（相对 ArmouryDevice） | 大小 | PE | 角色 |
|---|---|---|---|---|
| asus_framework.exe | 根目录 | 152 MB | Electron 29.4.6 x86 | 主进程宿主（含 Chromium/V8） |
| ArmouryMsSDK.dll | dll\MouseSDK\ | 1,215,320 B | PE32 x86 | 鼠标功能 SDK，XML 入口 |
| AC_UnifyDongleSDK.dll | dll\UnifyDongleSDK\ | 1,246,048 B | PE32 x86 | 接收器 SDK，直连 HID + WS 客户端 |
| ArmourySDK.dll | dll\ | 332,640 B | PE32 x86 | 框架 SDK（无 ExecuteFunction 导出） |
| ArmourySocketServer.exe | dll\ArmourySocketServer\ | 1,781,080 B | PE32 x86 | 本地 WebSocket 消息总线 |
| InstallHelper.dll | dll\ | 275,296 B | PE32 x86 | 安装/注册/卸载助手 |
| MsFWUpdate.dll | 根目录 | 445,792 B | PE32 x86 | 固件升级（HTTP + HID） |
| AsTaskSched.dll | dll\Schedule\、dll\Tools\ | 26,136 B | PE32 x86 | 计划任务注册 |
| HidInterruptHandle.dll | Firmware\6871\、Firmware\6862\FW\ | 93,600 B | PE32 x86 | HID 中断读写（带设备枚举） |
| InterruptTransfer.dll | Firmware\6871\ 等 | 90,672 B | PE32 x86 | HID 中断读写（无枚举） |
| AuraIC_x64.dll | dll\ArmourySocketServer\ | 2,878,816 B | PE32+ x64 | Aura 灯效 IC 控制 |
| ac_node_addon.node | resources\app.asar.unpacked\node_modules\ | — | 原生 Node 插件 | 主进程 ↔ 硬件桥 |

## 4.2 导出函数表（解析自 PE 导出目录）

### ArmouryMsSDK.dll（7 个导出）

| 序号 | 函数 | RVA | 说明 |
|---|---|---|---|
| 1 | Entrypoint | 0xA4E0 | 初始化入口（4 参数） |
| 2 | ExecuteFunction | 0xA4F0 | 唯一业务入口，XML in / out 指针 |
| 3 | Post_Entrypoint | 0xAF50 | 初始化后置（5 参数） |
| 4 | Post_EntrypointReturn | 0xAF60 | 后置返回值 |
| 5 | freeBuffer | 0xB030 | 释放返回缓冲 |
| 6 | startService | 0xB090 | 启动服务，返回 0 |
| 7 | timerCallback | 0xB290 | 定时器回调 |

### 其余模块

| 模块 | 导出 |
|---|---|
| AC_UnifyDongleSDK.dll | 同样 7 个（Entrypoint/ExecuteFunction/Post_Entrypoint/Post_EntrypointReturn/freeBuffer/startService/timerCallback），地址不同 |
| ArmourySDK.dll | 6 个：Entrypoint、Post_Entrypoint、Post_EntrypointReturn、freeBuffer、startService、timerCallback（**无 ExecuteFunction**） |
| InstallHelper.dll | 24 个：InstallASIO3Driver、InstallBongioviAPI、InstallDotNetFramework、InstallFilterDriver(Ex)、InstallKeyBotEnvironment、InstallNahimicAPI(ForSDK)、InstallVC2010/2017Redist、KillProcess、RegisterArmourySocketServer、RegisterLiveUpdate、Unregister…、Uninstall…、DeleteInstallationRecord、NeedUpdateBongiovi |
| MsFWUpdate.dll | 3 个：CheckFWUpdate、QueryDevice、UpdateDevices |
| AsTaskSched.dll | 2 个：RegisterScheduledTask、DeleteScheduleTask |
| HidInterruptHandle.dll | 10 个：GetHidHandle、GetHidHandleWithIndex、GetConnectDevice、GetConnectDeviceWithPID、CloseHidHandle、CreateReadEvent、InterruptTransfer_Write、InterruptTransfer_WriteLen、SetInterruptTransferReadEvent(…Len) |
| InterruptTransfer.dll | 7 个（同上，去掉 GetConnectDevice*） |

## 4.3 依赖矩阵（导入 DLL）

| 模块 | 关键导入 | 结构含义 |
|---|---|---|
| ArmouryMsSDK.dll | KERNEL32(166)、WS2_32(29)、CRYPT32(12)、WLDAP32(18)、ADVAPI32(30)、ole32、USER32、SHELL32、OLEAUT32、SHLWAPI、COMDLG32 | 会走网络栈与加密；**不导入 HID.DLL**——HID 由其他组件承担，SDK 只做协议与业务 |
| AC_UnifyDongleSDK.dll | KERNEL32(160)、**HID.DLL(7)**、SETUPAPI(6)、WS2_32(34)、CRYPT32(12)、WLDAP32(18) | 接收器侧直接操作 HID 设备 + 走 WS |
| ArmourySDK.dll | KERNEL32(94)、ADVAPI32、USER32、SHELL32、OLEAUT32、SHLWAPI | 纯框架/COM 层 |
| InstallHelper.dll | msi.dll、WTSAPI32、ADVAPI32、SHELL32、KERNEL32 | 安装期调用 MSI 与终端会话 API |
| MsFWUpdate.dll | cpprest141_2_10、WININET、urlmon、MSVCP140、HID.DLL、SETUPAPI、VERSION | 固件包下载（HTTP）＋ HID 烧写 |
| AsTaskSched.dll | MSVCR80、ole32、OLEAUT32、ADVAPI32 | Task Scheduler COM |
| HidInterruptHandle.dll / InterruptTransfer.dll | SETUPAPI、HID.DLL、KERNEL32 | SetupDi 枚举 + HidD_* 读写 |

> 结论：**HID 访问集中在 AC_UnifyDongleSDK / MsFWUpdate / HidInterruptHandle / InterruptTransfer**，ArmouryMsSDK 不含 HID 导入。

## 4.4 function key 分派表（ExecuteFunction 伪代码实证）

\`ExecuteFunction\` 反编译显示：解析 XML → 取 \`device_type\` → \`device\` → \`function\` → 用 \`stoi\`（基数 10）取 key → 长 if-else 链分派到内部实现函数。同时用 \`DAT_1011ca38\` 做**重入互斥**（同一时刻只允许一个操作），结束后清零。

| key | 内部实现函数 | 既有文档标注 |
|---|---|---|
| 0 | FUN_1003b1f0 | SetKeyChange |
| 1 | FUN_1003ce40 | SetPerformance |
| 2 | FUN_1003a330 | SetEffect |
| 3 | FUN_10039830 | SetCalibration |
| 4 | FUN_1003d610 | SetPower |
| 5 | FUN_1003d890 | SetProfile |
| 6 | FUN_1003c7b0 | SetLoadData |
| 7 | FUN_1003e4b0 | SetSync |
| 8 | FUN_1003ac60 | SetFrame |
| 9 | FUN_1003bc10 | SetKeyLog |
| 10 (0xA) | FUN_10014ab0 | Enumerate/Query |
| 11 (0xB) | FUN_1003e890 | SetWriteFile |
| 12 (0xC) | FUN_10039ca0 | DeleteFile |
| 13 (0xD) | FUN_10039170 | SendNotification/Event |
| 14 (0xE) | FUN_10038790 | （文档未列） |
| 15 (0xF) | FUN_10039b20 | SetDail |
| 16 (0x10) | FUN_1003b060 | SetJoystickMode |
| 17 (0x11) | FUN_1003ae20 | SetFwUpdate |
| 18 (0x12) | FUN_1003a1b0 | SetDeviceReset |
| 19 (0x13) | FUN_1003a020 | SetDemoMode |
| 20 (0x14) | FUN_1003e700 | SetWDL |

日志字符串实证：\`[ArmouryMsSDK]enter function %d\`、\`[ArmouryMsSDK]exit function %d\`、\`[ArmouryMsSDK]function %d fail\` / \`succese\`（原文拼写）。

**输入 XML 会被整段写入日志**：\`+++++strInputXml+++++\` … \`-----strInputXml-----\`，这是排查协议最省事的入口。

### SetLoadData（key=6）的实现骨架

`FUN_1003c7b0` 是**数据读取的二级分派**：
1. `FUN_100304d0` 解析 XML → 取 `data_number`
2. 日志 `[ArmouryMsSDK][SetLoadData]DataNumber %d`
3. 按 data_number 走约 25 个分支，各自调用不同的读取函数（`FUN_10017bb0`、`FUN_1001c1c0`、`FUN_1001d990`、`FUN_10019e20`、`FUN_10016750`、`FUN_1001de50`、`FUN_1001bdb0`、`FUN_1001aa60`、`FUN_1001a2e0`、`FUN_100199d0`、`FUN_1001d4e0`、`FUN_1001f930`、`FUN_10016fd0`、`FUN_10020b00`、`FUN_1001ca30`、`FUN_10015f00`、`FUN_10020730`、`FUN_1001b460`、`FUN_1001b8b0`、`FUN_1001b090`、`FUN_100162d0`、`FUN_10017360`、`FUN_100215b0`、`FUN_100202f0`、`FUN_10018760`)
4. 结果写入输出缓冲（`param_3`），由 ExecuteFunction 交回调用方

即「读电量/固件/rf 版本」不是一条命令，而是 `data_number` 选一的上百种数据项之一。

### SetPerformance（key=1）的调用链

\`FUN_1003ce40\` 的结构：\`FUN_10030d00(\`XML 解析\`)\` → \`FUN_10017960(this, param_1, xml)\` → \`FUN_1004cdb0(\`落地\`)\`。

**修正**：\`FUN_10017960\` 不是性能业务函数，而是**通用设备上下文获取**——SetLoadData（key=6，\`FUN_1003c7b0\`）在解析后调用的是同一个 \`FUN_10017960(this, param_1, xml)\`，用于按 PID 与 XML 取设备对象。业务逻辑在其后的分支里。

两处都出现 \`param_1 != 0x1824\` 的型号判断（0x1824 = 6180），说明该 PID 走特殊策略；P520 的 6871（0x1AD7）不在其中。

## 4.5 XML 业务字段全集（ASCII 字符串实证，含未公开项）

**信封与通用**

\`device_type\`、\`device\`、\`function\`、\`device_mode\`、\`model_name\`、\`device_config\`、\`dg_sn\`、\`display_name\`、\`live_update\`、\`type_error\`、\`other_error\`

**按键 / 宏**

\`macrokey\`、\`macrokey_uuid\`、\`dial_uuid\`、\`button_number\`、\`button_count_\`、\`button_function\`、\`button_function_\`、\`button_\`、\`key_change\`、\`key_0\`…、\`source_key\`、\`target_key_1\`…\`target_key_4\`、\`gesture_direction\`、\`gesture_function\`、\`gesture_key_1\`…\`3\`、\`windows_shortcut\`、\`print_screen\`、\`image_dir\`、\`cursor_position\`、\`horizontal_scroll\`、\`multi_keyboard_layout\`

**性能 / DPI / 轮询**

\`dpi_number\`、\`dpi_channel\`、\`dpi_onoff\`、\`angle_snapping\`、\`angle_tuning\`、\`polling_rate\`、\`polling_rate_wireless\`、\`button_response\`、\`high_performance_mode\`、\`dial_mutipller\`（原文拼写）、\`dial_sensitivity\`

**灯效**

\`effect_id\`、\`effect_number\`、\`color_number\`、\`color_count_\`、\`color_r\`/\`color_g\`/\`color_b\`、\`brightness_wireless\`、\`sync_function\`、\`sync_effect\`、\`sync_r\`/\`sync_g\`/\`sync_b\`、\`frame_effect\`、\`frame_count\`、\`Sync_Get_Frame\`

**校准 / 省电 / 配置**

\`calibration_function\`、\`calibration_material\`、\`calibration_height\`、\`calibration_result\`、\`powersavingmode\`、\`profile_index\`、\`profile_number\`、\`profile_function\`、\`profile_parameter\`

**数据 / 文件 / 固件**

\`data_number\`、\`data_name\`、\`file_name\`、\`file_data\`、\`on_off\`、\`event_index\`、\`keylog_state\`、\`keylog_event\`、\`fw_update\`、\`update_success_code\`、\`finished_code\`、\`download_link\`、\`rf_version\`、\`usb_version\`、\`stick_mode\`、\`booster_version\`、\`communication_interface\`、\`dll_name\`、\`support_function\`、\`idx_title\`

**其他**

\`left_click\`、\`right_click\`、\`display_\`、\`currently_display\`、\`all_area_flag\`、\`http_proxy\`/\`all_proxy\`/\`no_proxy\`（走 curl/HTTP 时的代理配置）

## 4.6 ArmourySocketServer.exe 的命令面（字符串实证）

- 传输：WebSocket++ 0.8.2，服务端；连接串模板 \`ws://127.0.0.1:\`，服务端目标 \`/?role=socketServer\`
- 单实例：\`Global\ArmourySocketServerMutex\`，重复启动报 \`ArmourySocketServer.exe is already running\`
- 日志：\`ArmourySocketServer.log\`，路径片段 \`MainSDK\ArmourySocketServer\`
- 设备热插拔：\`RegisterDeviceNotification\` / \`SetupDi*\`（\`UnregisterDeviceNotification Error\`）
- 命令处理器：\`GetAuraInformationCmdHandler\`、\`GetMacroListCmdHandler\`、\`GetMatrixSyncStateCmdHandler\`、\`SetMatrixSyncStateCmdHandler\`、\`IsAppExistCmdHandler\`、\`SendRapidFireToastCmdHandler\`
- 业务动词（发给 AC 或页面的消息）：\`GetDeviceProfileList\`、\`GetDeviceProfile\`、\`SetDeviceProfile\`、\`UpdateDeviceProfile\`、\`DeviceProfileChange\`、\`GetDeviceDescription\`、\`GetDeviceLightingControl\`、\`GetRelScenarioProfileList\`、\`GetScenarioProfileData\`、\`DeviceIn\`、\`DeviceOut\`、\`IsNewDevice\`、\`DevicePlugInNotify\`、\`FestivalEffect\`、\`QueryAllDeviceStatus(FromLS)\`、\`GetMatrixSyncState\`/\`SetMatrixSyncState\`、\`AcquireMatrixControl(FromLS)\`、\`UpdateWindowsLightingState\`、\`SendRegLevelToFramework\`、\`broadcastEvent\`
- 消息字段：\`command\`、\`commandType\`、\`command_type\`、\`device_type\`、\`dg_sn\`、\`dongleSN\`、\`deviceSN\`、\`device_name\`、\`profile_list\`、\`onboard_device_profile_list\`、\`software_device_profile_list\`、\`old_device_profile\`、\`new_device_profile\`、\`aura_device_type\`、\`related_scenario_profile_list\`、\`device_troubleshooting_list\`、\`connecteddevice\`、\`ingroupdevice\`、\`festival_effect\`、\`device_description\`、\`profile_list\`
- 外部数据：\`View\externalFiles\common\scenarioProfile\ScenarioProfileData.json\`（场景配置图标/列表）
- 多语言：\`dll\ArmourySocketServer\device_language\<Language>.xml\`
- 与 Armoury Crate 服务走 COM：\`CoInitializeEx\` / \`CoCreateInstance\` / \`QueryAllDeviceStatus\` / \`Query_MatrixControl_Status\`；\`GetDevicePluginGUID\` 依 AC 版本 \`>=6\` / \`<6\` 分支
- 版本语义：字符串 \`AC Version >= 6\` / \`AC Version < 6\`

## 4.7 AC_UnifyDongleSDK.dll 的消息面

- WebSocket++ 0.8.1 **客户端**，连接 \`ws://127.0.0.1:\`
- XML：\`device_type\` 取值 \`unify_dongle\`
- 消息字段：\`devicePage\`、\`deviceType\`、\`deviceSN\`、\`command\`、\`device_changed\`
- 同时内置 curl/schannel（HTTP/HTTPS 客户端，可下载固件）
- TinyXML（\`TiXmlDocument\`）与 nlohmann JSON（\`[json.exception.\`）共存

## 4.8 与既有逆向记录的差异（本次实测纠错）

| 既有记录 | 实测 | 依据 |
|---|---|---|
| \`AacMouseHal_x86.dll\` 实现 HID 协议 | **该文件在已装目录中不存在** | 全盘 \`C:\Program Files (x86)\ASUS\` 递归搜索无匹配；HID 由 AC_UnifyDongleSDK / HidInterruptHandle / InterruptTransfer 承担 |
| \`ArmouryHtmlDebugServer.exe\` 为页面宿主 | 本机未安装该文件 | 递归搜索无结果；页面由主进程 express 提供（\`/view/\` 路由） |
| 端口 1042/1043 | 主进程 JS 中同时出现 **8080** | \`data/asar-js-analysis.json\` 抽取结果 |
| P520 有专属 MouseSDK 型号目录 | \`dll\MouseSDK\` 45 个型号目录中**没有 P520** | 配置外置到 \`ProgramData\ASUS\ArmourySDK\MouseSDK\P520\` |
| HID 帧由 SDK 直接发出 | ArmouryMsSDK **不导入 HID.DLL** | PE 导入表 |

## 4.9 证据与限制

- 已用 Ghidra 完整分析：\`ArmouryMsSDK.dll\`（5,731 函数 / 3,050 字符串，27 段）、\`AsTaskSched.dll\`
- 已解析 PE 导出/导入：8 个模块（见 \`pe-analysis.json\`）
- 未做全量反编译：\`ArmourySocketServer.exe\`、\`AC_UnifyDongleSDK.dll\`（各约 1.2–1.8 MB，结论来自字符串与导入表，命令分派表未逐条落地）
- \`.NET\` 检测：上述组件均为原生 PE，无 CLR 头（ArmouryDevice 生态内 .NET 组件在 \`ArmouryDriverUpdate\`）
- 未验证：HID 子命令与 \`51 31\` 帧的字节级映射（需动态抓包或对 \`AC_UnifyDongleSDK\` 做完整反编译）

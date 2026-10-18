# 05 · 运行时架构与协议栈

> 把 01–04 的局部事实拼成端到端结构。证据：REA JS 静态分析、PE 导入表、字符串抽取、Ghidra 伪代码。

## 5.1 分层架构

~~~
[渲染层] View\p520 | View\omni | View\6871 | View\6862   (React + webpack)
    index.html -> index.js -> xx-bundle-<hash>.js ; service.js (utilityProcess fork)
        |  HTTP  GET /<PID>/index.html?isElectron=1&session=<uuid>
        |  WS    ws://127.0.0.1:1042/?role=<role>
[主进程] asus_framework.exe  .webpack/main/index.js (1.28 MB)
    express + helmet + WebSocketServer; 监听 1042 / 1043 / 8080
    路由 /api/:version/type/:deviceType/model/:modelNumber/:resource/:id  ;  /view/
    IPC   ipcMain.on(uwp | electron | log)
    模块  modules\mouse\index.js (83 KB), modules\unifyDongle\index.js (24 KB)
    原生  ac_node_addon.node
    SDK   ArmourySDK.dll / ArmouryMsSDK.dll / AC_UnifyDongleSDK.dll
        |  WS  /?role=socketServer          |  XML  ExecuteFunction
[总线] ArmourySocketServer.exe          [SDK] ArmouryMsSDK.dll (5,731 函数)
    WebSocket++ 0.8.2 服务端                  XML -> function key 0..20 分派
    互斥 Global\ArmourySocketServerMutex      重入互斥 DAT_1011ca38
    SetupDi 热插拔; COM 调 AC 服务             日志落 ProgramData ...\MouseSDK
        |
    AC_UnifyDongleSDK.dll (WS 客户端, 导入 HID.DLL + SETUPAPI)
        |  HID 65 B 报告
    Omni 接收器 6862  ->  2.4G 6871 / 蓝牙 6873  ->  ROG STRIX IMPACT III WIRELESS
~~~

## 5.2 进程与端口

| 端点 | 进程 | 协议 | 作用 |
|---|---|---|---|
| 1042 | asus_framework.exe | HTTP + WS | 页面宿主 + 主总线；端口取自注册表 HKLM\SOFTWARE\WOW6432Node\ASUS\ArmouryDevice\HTTPPort（默认 1042） |
| 1043 | asus_framework.exe | WS | 次总线，语义未落地 |
| 8080 | asus_framework.exe | HTTP | JS 字符串中出现，未观察到实际使用（推断调试/回退） |
| 9013 | asus_framework.exe | WS | 提示旁路 alertSocketServer |
| 9014 | asus_framework.exe | WS | 日志旁路 logSocketServer |
| 独立进程 | ArmourySocketServer.exe | WS 客户端/守护 | 设备事件总线，单实例；用 CreateToolhelp32Snapshot 看护 asus_framework.exe |
| 子进程 | utilityProcess(service.js) | 主进程 IPC | 每页面一个服务进程 |
| COM 客户端 | ArmourySocketServer | COM | 查询设备状态 / 矩阵灯效控制 |

结论：页面不直连驱动。链路是 页面 → 主进程(1042) → 服务进程/总线 → SDK → HID。

## 5.3 三套协议

### A. 页面 ↔ 主进程
- 加载：GET http://127.0.0.1:1042/<PID>/index.html?isElectron=1&session=<uuid>
- 资源：/api/:version/type/:deviceType/model/:modelNumber/:resource/:id
- WS 消息 JSON，camelCase 与 snake_case 混用：devicePage、deviceType、deviceSN、command、device_changed、command_type、device_type
- 事件名：socketServerEvent、broadcastEvent、DevicePlugInNotify
- WS role 取值：home / devicePage / deviceService / devicePageLogger / socketServer

### B. 主进程 ↔ SDK（XML）
- 入口：ExecuteFunction(LPCWSTR xml, IntPtr* pOut)；freeBuffer 释放
- 信封：<root><device_type key="1"><device key="{PID}" sn="{SN}"><function key="N" .../></device></device_type></root>
- 结构属性用 key="N"；业务字段必须是子元素文本，属性式触发 std::stoi 异常
- 字段全集见 04 章 §4.5
- 每次调用整段入日志：+++++strInputXml+++++

### C. SDK ↔ 设备（HID）
- WriteFile/ReadFile，65 字节（Report ID + 64）
- 帧头：**51 31 已证伪**（三处组件、25 处命中全是巧合：指令字节 / RVA 指针 / UTF-16 表）；真实报告格式需一次成功设置的运行时捕获
- 超时：写 1000 ms / 读 5000 ms / 命令 3000 ms
- 承载：AC_UnifyDongleSDK.dll（HID.DLL）、HidInterruptHandle.dll、InterruptTransfer.dll

## 5.4 三条数据流

~~~
① 设置 DPI
页面(6871) -> WS SetPerformance -> modules\mouse
  -> <function key="1"><dpi_number>..</dpi_number>..</function>
  -> ArmouryMsSDK.ExecuteFunction -> FUN_1003ce40 -> FUN_10017960 -> FUN_1004cdb0
  -> HID 帧 -> 接收器 -> 鼠标

② 读电量/固件版本
页面 -> WS -> <function key="6"> (SetLoadData) -> FUN_1003c7b0 -> 读 HID -> XML 回包 -> 页面

③ 固件升级
主进程 -> MsFWUpdate.dll: CheckFWUpdate / QueryDevice (HTTP, cpprest)
  -> UpdateDevices -> peripheral_fwu.exe | peripheral_fwu_pro.exe
  -> HidInterruptHandle.InterruptTransfer_Write -> 设备
~~~

## 5.5 页面路由与 PID

| URL 段 | 页面目录 | 含义 | 已安装 |
|---|---|---|---|
| /p520 | View\p520 | 产品页 | 是 |
| /omni | View\omni | 接收器主页 | 是 |
| /6871 | View\6871 | 2.4G 设备页（主设置页） | 是 |
| /6862 | View\6862 | 接收器设备页 | 是 |
| /6873 | — | 蓝牙设备页 | 否（仅安装包） |

## 5.6 落盘位置

| 内容 | 路径 |
|---|---|
| 鼠标配置 | C:\ProgramData\ASUS\ArmourySDK\MouseSDK\P520\ |
| 框架配置 | C:\ProgramData\ASUS\Framework\MouseSDK\P520\ |
| SDK 日志 | ...\ARMOURY CRATE Diagnosis\MouseSDK\YYYY-MM-DD.log |
| 框架日志 | ...\Diagnosis\Framework\<hash>\{View,Framework,Service}.XX.log |
| 固件升级日志 | ...\Diagnosis\MAFWUpdate\ROG STRIX IMPACT III WIRELESS\ |
| 总线日志 | ...\MainSDK\ArmourySocketServer\ArmourySocketServer.log |

## 5.7 启动链

~~~
登录 -> 计划任务 \ASUS\ArmourySocketServer
     -> ArmourySocketServer.exe（Global 互斥，起 WS 总线）
     -> 拉起 asus_framework.exe
     -> 监听 1042/1043/8080，装载 modules\mouse|unifyDongle、ac_node_addon、ArmourySDK.dll
     -> 页面请求 /<PID>/index.html?isElectron=1&session=...
     -> service.js 以 utilityProcess 起子进程并订阅 WS 事件

其他自启：\ASUS\AcPowerNotification、\ASUS\Framework Service
服务：ArmouryCrateDownloadTool（ArmouryLiveUpdate.exe）
~~~

## 5.8 与既有结论的差异

| 既有记录 | 实测 |
|---|---|
| AacMouseHal_x86.dll 实现 HID | 已装目录无此文件；HID 在 AC_UnifyDongleSDK / HidInterruptHandle / InterruptTransfer |
| ArmouryHtmlDebugServer.exe 是页面宿主 | 本机无此文件；页面由主进程 express 的 /view/ 提供 |
| 端口 1042/1043 | 另见 8080 |
| P520 有 MouseSDK 型号目录 | 45 个型号目录中无 P520，配置外置 ProgramData |
| 页面直接调 SDK | app.asar 全文 0 次 ExecuteFunction，实际由 ac_node_addon.node 内部调用 |

## 5.9 未验证

1. 1043 端口的确切服务语义
2. 8080 是否真正 listen
3. role=deviceService 的服务端归属
4. ExecuteFunction 的运行时调用点（原生插件内部）
5. HID 子命令与 51 31 帧的字节级映射（需动态捕获）
6. 接收器固件 6.00.25 与已装 7.00.08 的取舍

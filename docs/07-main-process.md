# 07 · 主进程与 service.js 代码实现逆向

**范围**：app.asar 内主进程（.webpack/main/index.js、sdk.js、renderer/window/preload.js、renderer/window/index.js）、View\6871\service.js、modules\mouse\index.js、modules\unifyDongle\index.js。

**方法**：只读。用 Node + original-fs 按 asar 头偏移读取，再从 **source map 的 sourcesContent 还原原始 TypeScript**（主进程 bundle 内含 466 个源文件，其中 22 个自家源码，其余为 express/helmet/ws/xml2js 等依赖）。下文行号若无特别说明，均指**还原出的原始 TS 文件行号**，括号内给出 asar 内路径。

## 0. 解析方法与一处格式修正

asar 头实际布局（本包实测，与常见资料「JSON 从 12 开始」不同）：

| 偏移 | 字段 | 本包值 |
|---|---|---|
| 0..3 | UInt32LE 固定 4 | 4 |
| 4..7 | headerSize（pickle size） | 207500 |
| 8..11 | headerSize-4 | 207496 |
| 12..15 | JSON 字节长度 | 207490 |
| 16.. | JSON 正文 | {"files":{...}} |
| dataStart | 8 + headerSize | **207508**（16+207490=207506，另有 2 字节 4 字节对齐 padding） |
| 文件总长 | — | 28,374,473 |

即：**JSON 起点 16，数据区起点 8+headerSize**。用 12 作 JSON 起点会读到 pickle 长度尾字节而 JSON.parse 失败（实测报 Unexpected token）。脚本必须 `require('original-fs')`，普通 fs 会把 app.asar 当虚拟目录拦截。

关键文件项（offset 相对 dataStart）：

| asar 路径 | offset | size |
|---|---|---|
| .webpack/main/index.js | 15,455,096 | 1,421,460 |
| .webpack/main/sdk.js | 20,867,166 | 254,574 |
| .webpack/renderer/window/preload.js | 22,581,088 | 2,302 |
| .webpack/renderer/window/index.js | 22,580,633 | 134 |
| node_modules/ac_node_addon/build/Release/ac_node_addon.node | — | 451,584（unpacked） |
| .sign/addon/ac_node_addon.node | — | 463,704（签名副本，非加载对象） |

全包 786 个文件项，仅 2 个 unpacked。package.json：`name=asus_framework, version=4.2.2.8, main=.webpack/main`。

---

## 1. 主进程启动序列

### 1.1 进程入口（src/index.ts）

```
process.env.NODE_PATH = pathMapping.proj;  UV_THREADPOOL_SIZE='128';  ELECTRON_DISABLE_SECURITY_WARNINGS='true'
app.commandLine.appendSwitch('lang','en-US');  app.disableHardwareAcceleration();
const singleLock = app.requestSingleInstanceLock();
```
（index.ts:15-27）

- 单实例：拿不到锁则只初始化日志并 `app.quit()`（index.ts:100-112）。
- `defineGlobalParams()` 把 9 个符号挂到 global：`globalRequire / logMessage / utility / registry / pathMapping / errorCode / BaseRouter / atob / btoa`（index.ts:118-133）。后续模块直接用全局名，无 import 关系——这是 webpack 能任意拆分的前提。
- `initLogger()` 先从注册表同步日志配置再建 logger（index.ts:114-117）；日志级别由 `HKEY_LOCAL_MACHINE\SOFTWARE\ASUS\ARMOURY CRATE Diagnosis\EnableLog` 决定（server.ts:58-80，值 ≥4 时开性能采样）。

### 1.2 app ready 后的顺序（index.ts:41-70）

1. `appLocker()` → `addon.requestMutex('asus_framework')`，失败抛 `F-1021 FRAMEWORK_EXISTED`（index.ts:107-113）——Electron 单实例锁之外的第二道跨进程互斥。
2. `server.construct()`：
   - 订阅日志注册表键（server.ts:58）；
   - `procManager = new ProcessManager(framework)`（server.ts:99）；
   - `deviceManager.init({routers, processManager})` → 注册 USB/BT/系统/HTML 更新四类回调（deviceManager.ts:104-110）；
   - 存在 `modules/library` 目录则覆盖内置 lib 路由，否则用 routers/lib.ts（server.ts:107-117）；
   - 装配 express 中间件与 5 个路由挂载点（server.ts:119-185，见 §2）。
3. `server.init()`（server.ts:198-246）：
   - `createServer({isSecure:false})` → HTTP **1042**；`createServer({isSecure:true})` → HTTPS **1043**（constant/index.ts:9-12；端口被占则退到动态端口 0）；
   - 两个 server 各自 `websocket.createServer(webServer)`（server.ts:394）；
   - 端口写注册表 `HKLM\SOFTWARE\ASUS\ArmouryDevice\HTTPPort / HTTPSPort`（server.ts:396-404）；
   - `checkModules()` 预扫描模块，**仅当返回非空**才 `checkMainSDKAlive()`（拉起 ArmourySDK 子进程），再逐个 `setPreloadProcess`（server.ts:217-227）；
   - 存在 `global.gc` 时每 60 s 强制 GC（server.ts:229-235）；
   - 最后 `deviceManager.loadDeviceServices()`：枚举已插设备、算 deviceType、按类型并行拉起设备 service 进程（deviceManager.ts:113-136）。
4. 仅当命令行带 `--view=<pid>` 才 `launchWindow(view, view)`（index.ts:64-69）；第二实例启动时解析 `--view=` 再开窗（index.ts:72-85）。

### 1.3 窗口与 BrowserView（modelApp.ts）

- 1 个 BrowserWindow（1280×720，frame:false，titleBarStyle:'hidden'，titleBarOverlay 高 33，#040404），`sandbox:false`（modelApp.ts:122-141）。
- **2 个 BrowserView**：
  - titleBar：`nodeIntegration:true, contextIsolation:false`，preload=.webpack/renderer/window/preload.js，`additionalArguments:['--session=<uuid>']`，加载 `file://<resources>/app.asar/.webpack/renderer/titleBar/index.html`，高 32（modelApp.ts:223-253；压缩 bundle 佐证 index.js@901007）。
  - devicePage：`nodeIntegration:true, contextIsolation:true`，同一 preload，加载 **HTTP 页面** `http://127.0.0.1:<HTTPPort>/<pid>/index.html?isElectron=1&session=<uuid>`（modelApp.ts:256-294）。
- 主窗口自身加载 `file:///…/renderer/window/index.html`（压缩后 index.js@900387）；该文件仅 134 B，只做 `__webpack_require__.ab='/assets/'`（renderer/window/index.js 全文）。
- 建窗 12 s 超时（`CREATE_WINDOW_TIMEOUT F-1023`，modelApp.ts:38-41）；close 被 preventDefault 并手动销毁两个 view（modelApp.ts:154-186）；`window-all-closed` 也 preventDefault（index.ts:87-89），进程不因关窗退出。

### 1.4 preload 暴露面（renderer/window/preload.js，2302 B）

`contextBridge.exposeInMainWorld('frameworkAPI', {...})` 成员表：

| 成员 | 行为 |
|---|---|
| webview.postMessage(str) | JSON.parse；失败只发 `ipcRenderer.send('log',{payload})`；成功 `ipcRenderer.send(Channel ?? 'uwp', {session, ...msg})` |
| webview.postMessageSync(str) | `ipcRenderer.sendSync(Target ?? 'electron', {session, ...msg})` |
| webview.subscribeEvent(cb) | 唯一监听通道 **'devicePage'**；校验 `msg.session === 本 view session`，`delete msg.session` 后 `cb(JSON.stringify(msg))`；每次先 removeListener 再 on，等价单订阅 |
| electron.postMessage / postMessageSync / subscribeEvent | 同上，默认 Channel/Target 均为 `'electron'`，subscribeEvent 额外记录 from→to 日志 |

`session` 取自 `new URLSearchParams(window.location.search).get('session') ?? ''`（preload.js module 977）。主进程侧对应 `ipcMain.on('uwp')` / `ipcMain.on('electron')`（interProcess.ts:30-32）。

---

## 2. Express 路由实现

### 2.1 中间件与挂载顺序（server.ts:119-185）

```
bodyParser.json({limit:'5mb'}) → bodyParser.text({limit:'5mb'}) → cors → 错误处理 → helmet(CSP)
→ express.json() → /alert → /lib/... → /type|/api/... → 静态 view 目录 → /:foreignLib
```

CORS 白名单只放主进程自身 listen 的 `http(s)://127.0.0.1:<port>`，dev 模式额外放 8080（server.ts:85-97, 407）。helmet CSP：`default-src 'self' blob: 127.0.0.1:*`，`connect-src` 允许 ws/wss/blob/api.giphy.com（server.ts:130-151）。

### 2.2 路由表（handler 与转发目标）

| 方法 + 模式 | 挂载点 | 有效 handler | 参数校验 | 转发到 |
|---|---|---|---|---|
| GET/POST `/alert` | server.ts:153 | socket.ts `Router.transferRequest`（30 s 超时） | POST 取 `req.body.data !== undefined`；GET 取 `req.query.msg`（server.ts:665-674） | `websocket.broadcastEvent({target:{role:['devicePage','alertPage','deviceService']}, msg})`；成功 200，否则 404 |
| GET/PUT/POST/PATCH/DELETE `/lib/:version/type/:deviceType/model/:modelNumber/:resource/:id?` 等 5 组 | server.ts:154-156 | `checkCommonLibrary` → `routerLib.handle` | 仅 favicon 特判 204（server.ts:497-501） | routers/lib.ts |
| `/lib/:version/devices`、`/lib/:version/window/:id`、`POST /lib/:version/:simpleResource` 等 | lib.ts:50-55 | 同上 | 资源白名单 | routers/lib.ts |
| GET/POST/DELETE `/api/:version/type/:deviceType/model/:modelNumber/:resource?/:id?` | server.ts:157-158 | `checkServiceAvailable` → `routers[deviceType].getRouter().handle` | `deviceType = foreignLib ?? params.deviceType`；进程状态 switch（server.ts:515, 530-601） | 设备模块（modules/mouse 或 common 兜底） |
| GET/POST/DELETE `/type/:deviceType/model/:modelNumber/:resource?/:id?` | server.ts:156-157 | 同上（先 lib 后 service，按 :resource 是否 file/image/collect 区分语义） | 同上 | 同上 |
| `/:foreignLib`（任意一段） | server.ts:185 | `checkServiceAvailable` | 段名当 deviceType 或外部 DLL 名 | 外部库路由（common.ts） |

### 2.3 设备路由内部（base/baseRouter.ts）

- 构造即注册 3 条默认模板：GET `/api/:version/type/:deviceType/model/:modelNumber/:resource?/:id?`、POST 同型（无 :id）、DELETE；handler 链 `timeout(默认 600000) → checkRequestQueue`（baseRouter.ts:96-101）。
- 模块可再 `setURL(list)` 追加（baseRouter.ts:130-144）；带 `hasFormData` 时先过 multer memoryStorage（88-90）。
- `checkRequestQueue`（baseRouter.ts:444-552）：
  - `sessionKey = uuid()`，日志 request received；
  - **resource === 'service'** 走服务分支：GET → 把当前端口写进 `query.host = '127.0.0.1:<1042|1043>'` 再 `executeService`；DELETE → `killService`（457-487）；
  - 其它：处理池空或 `hasProcessingLimit=false` 且非初始化中 → 立即 `transferRequest`；否则入队，队满 `maxQueue=10` 返回 `F-1033`（488-540）。
- `transferRequest`（146-194）：POST 时 body 原样当 XML（有 files 则多文件 buffer 拼成 utf16le 文本），然后 `processManager.send({cmd:'byPassRequest', method, deviceType, sessionKey, xml})`，回调 `dispatchResolve(sessionKey,status,payload)`；payload 为 string 时按 utf16le 回页面（205-213）。
- `executeService`（328-363）：读 `view/<modelNumber>/config.ini` 的 `[ArmourySDK] ServiceVersion`，有值才 `serviceManager.create(data)`（fork service.js），否则回 501 `F-1032`。

### 2.4 lib 路由（routers/lib.ts）

- 构造时 `setURL` 注册 15 条（lib.ts:36-55），超时 10–30 s。
- 资源分类：`regularResourcesList = ['file','image','collect']`，`simpleResourceList = ['devices','fonts','language','view','window','install','app','process']`（lib.ts:23-24）。
- 路径重写：URL 以 /file 结尾→`params.resource='file'`；/devices→simpleResource='devices'；/window→'window'（lib.ts:72-78）。
- 具体能力走 ac_node_addon：`isAppExist(id)`（416）、`getSystemFonts()`（436）、`getDefaultUILanguage()`（458）、`getConnectUsbDeviceList()/getConnectBtDeviceList()`（481-482）。

---

## 3. WS 服务端实现（utility/websocket.ts，单例 websocketManager）

### 3.1 建服

`createServer(server)` 对每个 http/https server 各 `new ws.Server({server})`，push 到 `this.servers`，`on('connection', handleOnSocketConnected)`（websocket.ts:21-27）。全包只有 1042/1043 两个 WS 入口；页面与 service.js 的 host 由主进程注入。

### 3.2 role 分派：不是 switch，而是「连接声明 + 广播过滤」

- 连接 URL 的 query 决定身份：`role, pid, deviceType, deviceSN, dongleSN`（websocket.ts:158-166）。`req.url.length <= 1`（无 query）→ role 记为 **'unknown'**，广播时对 unknown 一律投递（63-67, 188-193）。
- 连接记录：`{role, pid, deviceType, deviceSN, dongleSN, client}`（169-176）。
- 特例：`role==='socketServer'` 一接入即向所有 socketServer 广播 `{cmd:'30000' FRAMEWORK_CHECKER}`（178-187）。
- 消息入口带 sender `{client, role, modelNumber: pid}`（195-201）。
- 断线：从 wsConnections 移除；若同 pid 还有别的 role 存活则收集存活 role，然后 `emit('connectionClosed',{role, modelNumber, isAllClosed})`（202-238）→ server `handleOnSocketClosed` → `routers[deviceType].closeConnection(modelNumber, role, isAllClosed)`（server.ts:645-658）。
- 实际出现的 role 取值（代码证据）：`home`（deviceManager.ts:1046、1112）、`devicePage`+`deviceService`（deviceManager.ts:1138）、`devicePage`+`alertPage`+`deviceService`（server.ts:684、698、710）、`socketServer`（websocket.ts:178-181）、`deviceService` 与 `devicePageLogger`（service.js@470166 / @458880）、`unknown`（websocket.ts:190）。

### 3.3 消息解包与 command 分派表（websocket.ts:241-292）

```
evt(Buffer→string) → JSON.parse → const { command } = data → switch(command)
```

| command | 行为 |
|---|---|
| `broadcastEvent` | `this.broadcastEvent(data, sender)`；若 `msg.cmd === '10101'`（ALERTPAGE_INIT_STATUS_CHANGED）额外 `emit('deviceServiceFinished',{modelNumber:target.pid, deviceType:target.deviceType, result})`（253-268） |
| `writeLog` | `level` 2/3/4/5 → error/warn/info/debug，`deviceType` 作日志分域（269-279） |
| `socketServerEvent` | `emit('socketServerEvent', data)`（280-282） |
| `AcDiagnosisChange` | `logMessage.setLogLevel(data.enableLog)`（283-285） |
| 其它/解析失败 | 静默，仅 info websocket event format error（289-291） |

### 3.4 broadcastEvent 实现（websocket.ts:29-135）

1. `checkRole(item, target)`：target 为字符串则全等，为数组则 includes（30-36）。
2. 目标解构 `{role, deviceType, pid, deviceSN, dongleSN, modelNumber}`；`targetDevice = targetPID || targetModelNumber`（42-43）。
3. 遍历全部连接（60-118）：
   - role==='unknown' → 直接发；
   - 无 targetRole 或 role 不匹配 → 跳过；
   - 连接既无 pid 又无 deviceType → 直接发（源码注释：not sure who is using this case）;
   - 否则按 pid / deviceSN / dongleSN / deviceType 四项逐一比对，全中才发；目标未指定 pid 也未指定 deviceType 时该项视为通过（87-113）。
4. 消息体：字符串原样，对象 `JSON.stringify`（41）。
5. 若无任何连接命中且存在 sender，向 sender 回一帧 `{cmd:'20000' WEBSOCKET_TARGET_NOT_FOUND, ...target, msg}`，返回 false（120-134）——这是发送失败的唯一反馈。
6. 日志：`from = "<pid> <role>"`，`to = "<target> <targetRole>"`，含 cmd（54-58）。

---

## 4. 设备模块注册表

### 4.1 checkModules（server.ts:417-479）

```
moduleRootPath = <proj>/modules ; readdirSync 逐个目录
modulePath = modules/<folderName> ; result = __non_webpack_require__(modulePath)
const { deviceType, deviceName, router, shouldPreload, isLibrary } = result?.default ?? {}
```

- `isLibrary` 为真则整体跳过（库模块不参与设备注册）。
- `deviceName` 存在 → `procManager.setDeviceName(deviceType, deviceName)`。
- `router` 必须存在，否则抛 `F-1016 PLUGIN_FORMAT_ERROR`（日志记为 Module unavailable）。
- `routers[deviceType] = router(procManager)`；`shouldPreload` → 加入 preloadItems，init 阶段立即 `setPreloadProcess`。
- 用 `__non_webpack_require__` 直接 require 运行时目录，**模块不被打进 app.asar**，可热更新。

### 4.2 deviceType 的两级映射

1. **SDK 级**：`<proj>/dll/config.ini` 由 `parsePathConfig` 按 “Type ID / Path” 成对解析（processManager.ts:495-517；server.ts:262-267 读入并 setConfig）。实测 13 项：
   `1 → \MouseSDK\ArmouryMsSDK.dll`、`21 → \UnifyDongleSDK\AC_UnifyDongleSDK.dll`、2 KeyboardSDK、4 AudioSDK、7 MousePadSDK、5 AIOFanSDK、6 OledSDK、8 MBLedSDK、10 NodeSDK、9 SensorBoxSDK、13 VgaSDK、14 DisplaySDK、15 FanCardSDK。
   第 13 项写作 `Type ID=15`（无空格），解析仍可用。dll 目录实际只装了 ArmourySDK.dll / MouseSDK / UnifyDongleSDK / ArmourySocketServer / InstallHelper.dll 等。
2. **机型级**：`<proj>/view/<modelNumber>/config.ini` 的 `[ArmourySDK]` 段（deviceManager.ts:719-769）：
   `ArmourySocketServer / ServiceVersion / ForceLoading / DeviceType / AutoReload / ReloadS3 / USB / BT / DG / LEAUDIO`；
   `ArmourySocketServer !== 'true'` 直接忽略该机型；无 ServiceVersion 即「不支持 device service」；mode 由 pid 与 USB/DG/BT/LEAUDIO 四值比对得出，四值同时构成 `relatedPID`（同一物理设备的多 PID 候选）。
   实测 `View\6871\config.ini`：
   ```
   [ArmourySDK]  ArmourySocketServer = true  DeviceType = 1  APIVersion = 2  ServiceVersion = 1
   UWPVersion = 1  ReloadS3 = 1  DG = 6871  BT = 6873
   ```
   即 6871 是 2.4G 接收器 PID、6873 是蓝牙 PID、DeviceType=1（Mouse）。
3. `procManager.getDeviceTypeByPID(pid)` 用 `parseNameConfig` 解析各 SDK 目录下 config.ini 的机型清单（processManager.ts:519 起；调用点 deviceManager.ts:592, 656, 879, 1185）。deviceType 名称表在 constant/index.ts:120-140（`deviceNameMapping`：0 Main、1 Mouse、2 Keyboard、3 Accessory、4 Headset、5 AIO、7 Mousepad、8 MBLED、9 SensorBox、13 VGA、14 Display、15 FanCard、19 Gamepad、20 GameFirst、21 UnifyDongle、24 Booster、50 FanXpert）与 `peripheralDevice`（MOUSE:'1'、KEYBOARD:'2'、HEADSET:'4'、AIO:'5'、MOUSEPAD:'7'、MBLED:'8'、FANCARD:'15'、UNIFYDONGLE:'21'）。

### 4.3 实装模块与导出接口

`<proj>/modules` 只有 `mouse`、`unifyDongle` 两个目录（实测）。两者都是单行 webpack bundle，无 source map。

**modules/mouse/index.js**（83,428 B，入口 module 607，导出语句约在 bundle 偏移 46,350）：

```
t.default = { router: i.default, deviceType: u.MOUSE }   // u = constant.peripheralDevice → "1"
```

即导出 `{router, deviceType:"1"}`，无 deviceName/shouldPreload/isLibrary。Router 继承 BaseRouter，共 32 个方法（bundle 中 `prototype.X=` 去重）：

`transferRequest, initSDK, query, onDeviceRemoved, routingResource, initialize, initializeAlertPage, restorePreviewMode, resetAllProfile, removeFWProfiles, updateFW, repluginDevice, getWDLStatus, getWDLControl, setWDLControl, getDemoMode, setDemoMode, switchProfile, overwrite, setButtons, setLighting, setPerformance, setCalibration, setDialSetting, setJoystickMode, setPower, getProfile, deleteProfile, getDeviceInfo, getSystemInfo, subscribeEvent, bypassRequest`

setURL 仅 4 条（`/type/:deviceType/model/:modelNumber/:resource/:id?` 与 `/api/:version/...` 的 GET/POST 变体）。共享常量在 module 248：

```
commandCode: {POWER_INFO:"20101"}
functionNumber: KEY_CHANGE:"0" PERFORMANCE:"1" EFFECT:"2" CALIBRATION:"3" POWER:"4" PROFILE:"5"
                LOAD_DATA:"6" SYNC:"7" FRAME:"8" KEY_LOG:"9" QUERY:"10" WRITE_FILE:"11"
                DELETE_FILE:"12" EVENT:"13" DIAL_SETTING:"15" JOYSTICK_SETTING:"16"
                FW_UPDATE:"17" DEVICE_RE_PLUGIN:"18" SET_DEMO_MODE:"19" SET_WDL_CONTROL:"20"
dataNumber: DEVICE_INFO:"0" BUTTON_INFO:"1" PERFORMANCE_INFO:"2" EFFECT_INFO:"3"
            CALIBRATION_INFO:"4" POWER_INFO:"5" ... DONGLE_DEVICE_INFO:"10" MOUSE_IS_ALIVE:"11"
            SYSTEM_INFO:"14" WINDOWS_LIGHTING:"16" ... GET_DEVICE_SN:"24"
```

functionNumber 是 XML 里 function number 字段的取值来源，也是「页面动作 → HID 命令」的第一级分派号（推断：与 04 章 Ghidra 侧分派表对应，未在二进制侧逐项核对）。

**modules/unifyDongle/index.js**（23,896 B）：导出 `{router, deviceType: u.UNIFYDONGLE}` = `{router, deviceType:"21"}`；7 个方法：
`transferRequest, initSDK, routingResource, initialize, query, getDeviceInfo, bypassRequest`；路由 2 条（`/api/:version/type/:deviceType/model/:modelNumber/:resource/:id?` 的 GET/POST）。

---

## 5. JSON→XML 转换 parseJSON2XML

实现本质只有一行：**xml2js 的 Builder 包装**（utility/index.ts:90-102）：

```
const builder = new xml2js.Builder({ cdata: true, xmldec: {version:'1.0', encoding:'UTF-8', standalone:true} });
const xml = (json) ? builder.buildObject(json) : '';
```

字段映射规则（全部由 xml2js 决定，代码无自定义映射）：

| JSON 形态 | XML 结果 |
|---|---|
| `{a:{b:'1'}}` | `<a><b><![CDATA[1]]></b></a>`（cdata:true 使所有文本进 CDATA） |
| `{a:[x,y]}` | `<a>…</a><a>…</a>` 同名重复元素 |
| `{a:{$:{key:'1'},…}}` | `<a key="1">…</a>` —— `$` 子对象即属性集 |
| `{a:{_:'text'}}` | `<a>text</a>`（CDATA 包裹） |

**key 属性的生成**：调用点自行拼 `$`。唯一构造点 `broadcastWebsocketXML`（utility/index.ts:350-367）：

```
const xmlObject = { root: { device_type: { $: { key: deviceType },
                    device: { $: { key: modelNumber }, ...content } } } };
```

即固定外壳 `<root><device_type key="<deviceType>"><device key="<modelNumber>">…</device></device_type></root>`，业务字段以 `...content` 平铺进 device。设备模块同样手写 `$:{key}`（如 mouse bundle 中 `color_count_1:{$:{key:"1"}, color_r:"0", …}`）。

反向 `parseXML2JSON`：`parseString(str,{explicitArray:false})` 后只取 `jsonData.root`（utility/index.ts:104-118）——包根固定 root，与上面构造一致。service.js 侧同样取 `t.device_type.device`（service.js@470317）。

---

## 6. DLL 加载方式

### 6.1 三条独立加载路径

| DLL | 加载者 | 代码证据 |
|---|---|---|
| **ArmourySDK.dll**（框架主 SDK，Type 0） | sdk.js 子进程内 BaseSDK | `new addon.Library(<proj>/dll/ArmourySDK.dll)`（baseSDK.ts:16, 114） |
| **ArmouryMsSDK.dll**（鼠标 SDK，Type 1） | 同一 sdk.js 子进程，仅 `--targetSDKPath` 不同 | 路径来自 dll/config.ini 的 `\MouseSDK\ArmouryMsSDK.dll`，经 processManager.ts:353-358 传入 |
| **ac_node_addon.node**（原生插件，N-API 而非 ffi） | 主进程与 sdk 子进程均直接 require | 打包标记 `commonjs2 "ac_node_addon"`；实际文件 node_modules/ac_node_addon/build/Release/ac_node_addon.node（unpacked，451,584 B） |

**没有 ffi-napi / node-ffi / koffi**。`addon.Library` 是 ac_node_addon 自带的动态库封装（等价 LoadLibrary + GetProcAddress），package.json 里 ac_node_addon 是唯一原生依赖。

### 6.2 加载与绑定序列（sdk/src/base/baseSDK.ts）

```
preloadSDK():
  foreignLib = new addon.Library(foreignSDKPath)            // 114
  foreignLib.startService(0, (err, status) => resolve())    // 131-139  ← 首参固定 0，回调 (err,status)
transferPostRequest(data):
  xmlContent = atob(content)                                 // 41  base64 解码
  fileBuffer = Buffer.from(xmlContent, 'utf16le')            // 42
  foreignLib.Post_EntrypointReturn(xmlContent, (err, status, data) => ...)  // 66  ← 回调 (err,status,data)
```

- 导出函数名是**硬编码属性访问**：`Post_EntrypointReturn`、`startService`；缺失时回 `S-1003 SDK_RETURN_FAILED` 并记录 import … function … Post_EntrypointReturn, Post_Entrypoint（baseSDK.ts:92-97）。
- 调用签名统一为「入参字符串 + Node 风格回调」，addon 内部把返回字符串与状态码经回调抛出。
- 41-42 行的 `atob` 是全局名（index.ts:130 挂到 global.atob），不是浏览器 atob；`content` 由主进程用 `btoa(xml)` 编码（process.ts sendMessage）。

### 6.3 子进程与验签

- 拉起 SDK 前先验签：`acAddon.isWinVerifyTrust(<proj>/dll/<path>)`，失败置 `EProcessStatus.UNVERIFIED` 并 `rej(S-1011 SDK_UNVERIFIED)`（processManager.ts:142-153）。HTTP 侧对 UNVERIFIED 回 500 `S-1011`（server.ts:592-597）。
- `SDKProcess` 用 `utilityProcess.fork(sdk.js, ['--deviceType=','--targetSDKPath=','--projPath='])`（process.ts forkProcess）。
- 状态机：`UNKNOWN → INIT → READY → DOWN/FAILED/UNVERIFIED`；执行失败三次重试后抛 `EXECUTE_RETURN_FAILED`（processManager.ts:295-329、352-440）。

---

## 7. service.js 结构（View\6871\service.js，621,102 B，2 行）

### 7.1 入口与启动参数

- 由主进程 `Services.Service.init()` 启动：`utilityProcess.fork(<proj>/view/<modelNumber>/service.js, props)`，`props` 是 query 参数拼成的 `key=value` 数组（services.ts:57-69）。触发条件：`GET /api/:v/type/:dt/model/:pid/service`（baseRouter.ts:457-476），此时主进程已把 `host=127.0.0.1:<port>` 塞进 query。
- service.js 自解析：`process.argv.reduce((acc,arg)=>{const [k,v]=arg.split('='); acc[k]=v; return acc;}, {})`（bundle 尾部 rc），再 `nc(rc)` → `new Service(rc)`；导出 `{service, configSettings, constantMouse}`（bundle 尾部 `const ac={service:nc,configSettings:oc,constantMouse:D}; module.exports=i`）。
- 进程名：`Device Service process - <modelNumber>`（services.ts:75）。

### 7.2 与主进程的握手

- **WS 反向连接**（bundle@470,161）：
  ```
  new WebSocket(composeURL("<host>?role=deviceService&deviceType=<dt>&pid=<pid>", {dongleSN, deviceSN}, true))
  ```
  service.js 是**客户端**，连回主进程 1042/1043 的同一 WS 服务端；`onmessage`：JSON → `handleFrameworkEvent`，非 JSON（XML 回包）→ `xmlParser` → `t.device_type.device` → `handleSDKEvent`。
- **HTTP 回连主进程**（bundle@60,592）从 settings 派生：
  ```
  baseUrl = "http://" + (location.host || argv[host=] || "127.0.0.1:1042")
  ws      = "ws://" + host
  url     = baseUrl + "/api/<apiVersion>/type/1/model/6871"
  libUrl  = baseUrl + "/lib/<libVersion>/type/1/model/6871"
  dataCollectUrl = libUrl + "/collect"
  ```
  即 service.js 反向调用 §2 的 lib 路由做文件/图片/数据采集。
- **第二通道**：设备常量内 `alertSocketServer:"ws://127.0.0.1:9013"`、`logSocketServer:"ws://127.0.0.1:9014"`（bundle@19,534）；另有 `?role=devicePageLogger&deviceType=&pid=` 连接 logSocketServer（bundle@458,880）。**9013/9014 属 SocketServer 进程，不在 app.asar 内**（推断：对应 dll/ArmourySocketServer）。

### 7.3 消息 schema

| 方向 | 形态 |
|---|---|
| 主→service（WS 文本） | XML：`<root><device_type key="1"><device key="6871"><cmd><![CDATA[…]]></cmd>…</device></device_type></root>`（与 §5 同构；service.js 直接读 t.device_type.device） |
| 主→service（WS JSON） | 广播消息 `{cmd?, …}`，由 handleFrameworkEvent 处理（命令号与 §3.3 一致，如 10101/30000/30001/30002） |
| service→主（WS） | `{command:"broadcastEvent", target:{role,deviceType,pid,dongleSN}, msg:{cmd,result}}`；`msg.cmd==='10101'` 触发主进程 deviceServiceFinished（websocket.ts:253-268） |
| service→主（HTTP） | §7.2 的 url/libUrl，body 为 XML 或 JSON |
| service→sdk | `sendSocketServer({xml})`；内部 `promisePool` + `dispatchResolve({cmd,payload,fn,errorCode})` 按 cmd 关联（bundle@469,016） |

### 7.4 caps（能力描述）读取

- **不在主进程读取**：app.asar 内只有类型声明 `typings/types.ts:110 caps?: ICaps`，没有读 caps 文件的代码；`F-1010 CAPS_NOT_FOUND` 仅作预留错误码。
- service.js 把 caps 直接编译进 bundle：`settings.caps = {device:"ROG STRIX IMPACT III WIRELESS (2.4GHz)", deviceZH:"ROG 影刃 3 无线版", guid:"2e3ccb5f-…", productName:"P520", deviceType:"1", theme:"rog", multiModelNumber:["6871","6873"], profileCount:5, profiles:{hasRapidFireProfile:true}, deviceProps:{isWireless,isAuraSyncEditable,isSupportMacroShift,isSupportCustomKeyCode}, functionList:[{id:"Buttons",…}…]}`（bundle@3,406 起）。
- 同处还有 `version:"4.05.38"`、`url:"type/1/model/6871"`、`modelNumberMapping:{dongle:"6871", bluetooth:"6873"}`，以及多语言 `keyDefinitions` 键位矩阵（de-DE/en-US/…）。
- 结论：**能力集是构建期常量**，随 service.js 版本走，不随设备动态变化（推断：多机型靠 multiModelNumber 复用同一份 caps）。

### 7.5 错误码表

service.js 内置两份（主进程同名表 + 页面级追加）：

- 主进程同名表（bundle@506,877）：`S-1000…S-1011`、`F-1010…F-1041`、`F-1100…F-1204`，与 constant/index.ts:40-105 完全一致。
- 设备/页面级追加表 `Ee`（bundle@406,847）：
  ```
  INIT_WAIT_TIMEOUT:"2005" INIT_FAILED:"2006" DEVICE_NOT_SERVICE:"2008" DEVICE_NOT_FOUND:"2009"
  SDK_CRASH:"2011" SDK_RETURN_FAILED:"2012" AURA_SYNC_FAILED:"3001" AURA_UNSYNC_FAILED:"3002"
  GET_MATRIX_FAILED:"3003" MATRIX_SYNC_FAILED:"3004" MATRIX_UNSYNC_FAILED:"3005"
  ICAFEMODE:"4001" AURASYNC:"4002" DEVICEISALIVE:"4003" BOOTLOADERMODE:"4004" …
  ```

主进程命令号（constant/index.ts:20-29）：`20000 WS_TARGET_NOT_FOUND、30000 FRAMEWORK_CHECKER、30001 DEVICE_PLUGGED、30002 DEVICE_UNPLUGGED、30003 DEVICE_SERVICE_FAILED、30010 S0_RESUME、30011 S3_RESUME、30020 HTML_UPDATED、40001 DATA_COLLECTION_CLEARED`；另有 `10101 ALERTPAGE_INIT_STATUS_CHANGED`（websocket.ts:259 与 service.js）。

### 7.6 持久化路径与格式

主进程只往一处写业务文件（utility/index.ts:120-260）：`<ALLUSERSPROFILE>\ASUS\Framework\`，即 `C:\ProgramData\ASUS\Framework\`；恢复态子目录 `Restore\`。API：`writeFile(fileName, data, {encoding='utf8', isRestorePath})`、`readFile`、`writeINI(file, data)`。

- INI 序列化（writeINI，utility/index.ts:302-320）：`[modelName]\r\nkey = value\r\n`，编码 **utf16le**，无引号无转义。
- `dataCollectPath = <ProgramData>\ASUS\ARMOURY CRATE Diagnosis\ACPeripheralData`（routers/lib.ts:34）。
- 注册表：`HKLM\SOFTWARE\ASUS\ArmouryDevice`（HTTPPort/HTTPSPort）、`HKLM\SOFTWARE\ASUS\ARMOURY CRATE Diagnosis\EnableLog`。
- service.js 自身未见直接写 ProgramData 的代码（未命中 ProgramData / ASUS/Framework 字面量），其状态落盘应经 §7.2 的 libUrl 交回主进程（推断）。

---

## 8. ac_node_addon.node 在主进程中的使用点

| 调用 | 位置 | 语义 |
|---|---|---|
| `addon.requestMutex('asus_framework')` | src/index.ts:110 | 跨进程命名互斥体，返回布尔 |
| `addon.registerUsbChangedEvent(cb)` | deviceManager.ts:104 | USB 插拔回调（载荷 type/data.vidpid） |
| `addon.registerBtChangedEvent(cb)` | deviceManager.ts:105 | 蓝牙连接变化 |
| `addon.registerSystemEvent(cb)` | deviceManager.ts:106 | 系统 S0/S3/S4 恢复，type '1'/'2' |
| `addon.registerHtmlUpdateEvent(cb)` | deviceManager.ts:108-110 | 页面热更新，触发 commandCode.HTML_UPDATED 30020 广播 |
| `addon.isWinVerifyTrust(path)` | processManager.ts:143-144 | DLL Authenticode 校验，失败阻断加载 |
| `addon.forkProcess(webviewPath, modelNumber)` | utility/index.ts:454 | 拉独立 webview 进程承载部分页面 |
| `addon.isWDLSupported()` | utility/index.ts:544 | Windows Dynamic Lighting 支持探测 |
| `addon.isAppExist(id)` | routers/lib.ts:416 | /app 资源：外部应用是否安装 |
| `addon.getSystemFonts()` | routers/lib.ts:436 | /fonts 资源 |
| `addon.getDefaultUILanguage()` | routers/lib.ts:458、interProcess.ts:289 | 系统 UI 语言 |
| `addon.getConnectUsbDeviceList()/getConnectBtDeviceList()` | routers/lib.ts:481-482；deviceManager.ts:538 起 | 枚举当前连接设备 |
| `reg.register/unregister/getString/getDWord/setRegistry` | utility/registry.ts:31-124 | 注册表读写与变更订阅（reg 即同一 addon） |
| `new addon.Library(path)` | sdk/base/baseSDK.ts:114 | 动态库加载（§6） |

传参特征：**全部是字符串路径/名称 + 回调或同步返回值**，无结构化 buffer 传参；设备事件载荷为 `{type, data:{vidpid, …}}`（deviceManager.ts:1064-1090 解构，`type==='1'` 表示插入）。

---

## 9. 主进程模块依赖图

```
app.asar/.webpack/main/index.js  (Electron 主进程入口, 1,421,460 B)
├─ constant/index.ts            errorCode / commandCode / ports(1042/1043) / deviceNameMapping / peripheralDevice
├─ constant/pathRevise.ts       proj = dirname(process.execPath) → <ASUS>\ArmouryDevice
│                               view/lib = <proj>/view|dll ; ssl = <proj>/ssl
├─ utility/index.ts             parseParams / parseJSON2XML / parseXML2JSON / writeFile(readFile,writeINI)
│                               broadcastWebsocketXML / responseRequest / atob,btoa / getKeyNumberByName
├─ utility/registry.ts          → ac_node_addon (reg.*)         ← HKLM\SOFTWARE\ASUS\*
├─ utility/logger.ts            log4js 封装，logMessage(全局)
├─ utility/websocket.ts         ws.Server ×2 (1042/1043)；broadcastEvent；role 过滤；command 分派
├─ utility/interProcess.ts      ipcMain('uwp'|'electron'|'log') + channel{mainWindow,titleBar,devicePage}
├─ utility/sharpImage.ts        sharp 图片处理（lib /image）
├─ base/baseController.ts       EventEmitter 基类（onCustomEvent/fireEvent）
├─ base/baseRouter.ts           express.Router；默认 3 条 /api 模板；requestQueue(≤10)/processingPool
├─ routers/common.ts            Router(hasProcessingLimit=false) —— 无专用模块时兜底
├─ routers/lib.ts               /lib 资源：file|image|collect + devices|fonts|language|view|window|install|app|process
├─ routers/socket.ts            /alert（GET/POST 30 s）→ emit 'websocket'
├─ modelApp.ts                  BrowserWindow ×1 + BrowserView ×2(titleBar, devicePage)
├─ processManager.ts            sdkList(dll/config.ini) / processStatus / send() / 性能采样(pidusage)
├─ process.ts                   SDKProcess: utilityProcess.fork(sdk.js, --deviceType --targetSDKPath --projPath)
├─ services.ts                  Service: utilityProcess.fork(view/<pid>/service.js, key=value…)
├─ deviceManager.ts             USB/BT/系统事件 → devices{} → launchService / killService
├─ server.ts                    express 装配 + HTTP/HTTPS listen + checkModules + checkServiceAvailable
└─ .webpack/main/sdk.js         (SDK 子进程入口, 254,574 B)
   ├─ base/baseSDK.ts           new addon.Library(dll/<targetSDKPath>) → startService / Post_EntrypointReturn
   └─ utility/index.ts(同源)     sleep / bmpDecoder 等

外部（非 asar）
├─ <proj>/dll/config.ini        Type ID → DLL 相对路径（13 项）
├─ <proj>/dll/ArmourySDK.dll    框架主 SDK（Type 0，baseSDK 默认路径）
├─ <proj>/dll/MouseSDK/ArmouryMsSDK.dll   Type 1
├─ <proj>/dll/UnifyDongleSDK/AC_UnifyDongleSDK.dll  Type 21
├─ <proj>/modules/{mouse,unifyDongle}    __non_webpack_require__ 动态加载，导出 {router, deviceType}
├─ <proj>/view/<pid>/{index.html,…,config.ini,service.js,MD5.ini}
└─ node_modules/ac_node_addon/build/Release/ac_node_addon.node (unpacked)

运行时进程树
main(Electron) ─┬─ SDK 子进程 ×N  (sdk.js --targetSDKPath=…)
                ├─ DeviceService ×M (view/<pid>/service.js)  ──WS(role=deviceService)→ main:1042
                └─ webview 进程 (ac_node_addon.forkProcess)
客户端：页面(role=home|devicePage|alertPage)、SocketServer(role=socketServer, 9013/9014)、
        service.js(role=deviceService|devicePageLogger)
```

---

## 10. 与既有文档的差异 / 未验证项

1. **9013 / 9014 端口**：service.js 内固定 `alertSocketServer=ws://127.0.0.1:9013`、`logSocketServer=ws://127.0.0.1:9014`，app.asar 中无这两个端口字面量 → 归属 SocketServer 进程（其实现未验证，不在本轮范围）。
2. **1043 的用途**：HTTPS 服务端完整存在（selfsigned 365 天证书），但页面/service.js 的 host 默认常量是 127.0.0.1:1042；HTTPS 在何处被选用未找到分支（未验证）。
3. **caps 读盘**：本轮确认 caps 为 service.js 内常量；若存在外部 caps 文件覆盖逻辑，应在页面 bundle（task-5 范围）。
4. **functionNumber → SDK 分派**：modules/mouse 的 functionNumber 常量与 04 章 Ghidra 侧 ExecuteFunction 分派是否一一对应未逐项核对（推断对应）。
5. **原始 TS 行号来源**：引自 app.asar 内 .webpack/main/index.js.map 的 sourcesContent（466 项），不依赖外部 checkout；`D:\Workspace\ArmouryCrate\AC_Backend_Framework\` 只是 map 中的原始构建路径。
`
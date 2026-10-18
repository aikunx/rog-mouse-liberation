# 08 · ArmouryDevice 页面 bundle 代码实现级逆向

> 分析对象：`C:\Program Files (x86)\ASUS\ArmouryDevice\View\` 下 6871（主设置页）、p520（产品页）、6862（接收器页）、omni（接收器主页）。
> 本轮为**代码实现级**：只读、未执行任何 JS/exe、未修改安装目录；所有结论以「文件 + 字符偏移 / 模块 id / 键名」为证据。
> 未与 03 章重复目录盘点；04/05/06 章已覆盖的二进制与服务端内容不重复，只在衔接处引用。

## 0. 阅读约定

偏移均为**文件内 UTF-16 字符索引**（Node `readFileSync("utf8")` 的 `indexOf` 结果），由「正则 + 上下文窗口」提取，未格式化整个 bundle。

| 简称 | 文件 |
|---|---|
| `I` | `View\6871\index.js`（903,964 B，2 行压缩） |
| `B606` | `View\6871\606-bundle-e147c.js`（135,535 B） |
| `B989` | `View\6871\989-bundle-e147c.js`（801,093 B） |
| `B219` | `View\6871\219-bundle-e147c.js`（6,575 B） |
| `S` | `View\6871\service.js`（621,724 B，由主进程 utilityProcess.fork 拉起，非页面模块） |
| `C` | `View\6871\resources\src\_ref\caps.json`（8,962 B） |
| `D` | `View\6871\resources\src\_ref\default.json`（9,783 B） |
| `L` | `View\6871\resources\English-68efc.xml`（149,820 B） |

约定：**推断** = 由代码结构推出但无运行验证；**未验证** = 需运行时确认。

---

## 1. 路由与页面骨架

### 1.1 三层加载：壳 → 模型包 → 分包

页面没有 react-router：路由由 webpack `require.context` + `React.lazy` + Redux `currentPage` 三者拼成。

1. `index.html` 只有一个 `<div id="root">` + `index.js`。
2. `index.js` 是**通用壳**（顶栏 / Profile 切换 / Tab 导航 / Toast / 遮罩 / i18n / Redux），末尾按 URL 路径首段决定设备模型：

   ```js
   $i = window.location.pathname.split("/")[1]        // I@899.6k，如 "6871"
   a(3812)("./" + $i + "/caps")   a(3812)("./" + $i + "/default")
   ```

3. 设备页组件本身是 React.lazy：

   ```js
   Rn = Ln.settings.modelNumber
   Fn = i.lazy(function(){ return a(3812)("./" + Rn) })   // I@805112
   ```

4. `3812` 是上下文模块（`I@696422`），把「模块路径 → [模块 id, chunk…]」写死：

| context 键 | 值（模块 id, chunk） | 含义 |
|---|---|---|
| `./6871` / `./6871/index.jsx` | `[3672, 989, 606]` | 设备页主组件 = `B606` 模块 3672，依赖 `B989` |
| `./6871/caps` | `[2475, 475]` | caps 数据（475 分包 7,976 B） |
| `./6871/default` | `[6762, 762]` | default 数据（762 分包 3,591 B） |

5. 分包命名由运行时生成：`a.u = e => e + "-bundle-" + a.h().slice(0,5) + ".js"`，`a.h()` 返回 `"e147c35d0d5cebb8e1d6"`（`I@741544`、`I@741667`）→ 与磁盘 `*-bundle-e147c.js` 一一对应。

**分包分层实测**（chunkId 取自各文件头 `push([[chunkId],…])`）：

| 分包 | B | chunk | 模块数 | 角色 |
|---|---|---|---|---|
| `989-bundle-e147c` | 801,093 | 989 | 73 | 通用组件库：键盘按键图（模块 3551）、色板、滑杆、弹窗、工具函数 |
| `606-bundle-e147c` | 135,535 | 606 | 7 | **7 个功能页的全部实现** + SCSS 类名映射 + 键盘布局表 context（模块 2005） |
| `219-bundle-e147c` | 6,575 | 219,502 | 3 | 6871 专用「快捷键说明」弹窗（模块 7219） |
| `475-bundle-e147c` | 7,976 | 475 | 1 | caps 数据（模块 2475） |
| `762-bundle-e147c` | 3,591 | 762 | 1 | default 数据（模块 6762） |
| `502-bundle-e147c` | 564(js) | 502 | 1 | 样式模块 6502 |
| `494-bundle-e147c` | 117 | 494 | 1 | 空桩（模块 2494） |

### 1.2 Tab 结构与组件映射

Tab 列表**不是写死的**，由 `caps.functionList` 生成（`C` 中 7 项，`I@1732` 处同内容内联）：

```js
functionList:[{id:"Buttons",langRef:"row_2"},{id:"Performance",langRef:"row_3"},
 {id:"Lighting",langRef:"row_4"},{id:"Calibration",langRef:"row_5"},
 {id:"Power",langRef:"row_207"},{id:"Promotion",text:"ArmouryCrateGear",langRef:"Armoury Crate Gear"},
 {id:"FirmwareUpdate",langRef:"row_77"}]
```

Tab → 组件映射在 `B606` 的一个 PureComponent.render 里（`B606@127342`）：

| currentPage | 组件变量 | 定义位置（B606） | 打包位置 |
|---|---|---|---|
| Buttons | `Nt` | @83261 | 606 |
| Performance | `tn` | @108167（含 DPI 滑杆 xt、轮询 Ut） | 606 |
| Lighting | `Bn` ← `Rn` | @122592 | 606 |
| Calibration | `gn` | @111625 | 606 |
| Power | `kn` | @117562 | 606 |
| Promotion | `_n` | @119283 | 606 |
| FirmwareUpdate | `Pn` ← `Tn` | @122534 | 606 |

```js
switch(this.props.currentPage){
  case"Buttons":e=Nt;break; case"Performance":e=tn;break; case"Lighting":e=Bn;break;
  case"Calibration":e=gn;break; case"Power":e=kn;break; case"Promotion":e=_n;break;
  case"FirmwareUpdate":e=Pn;break; default:e=null}            // B606@127342
```

6871 的 Buttons 页还会再懒加载一次「模型专用」上下文（`B606@134584` 模块 9837）：

```js
I.lazy(function(){ return n(9837)("./" + (I||S)) })    // 606 模块 3672 顶部
// 9837: {"./6871":[7219,9,219], "pages/buttons/models/6871":[7219,9,219], "./6871/style":[6502,7,502]}
```

### 1.3 Tab 显示规则与进页副作用

壳组件（`I@805700` 起）按设备能力**过滤** Tab：

```js
(w&&!c||u) && (S = S.filter(e => "LightingSyncControl"!==e.id))   // 无 WDL 支持 / iCafe
(v||E)     && (S = S.filter(e => "Promotion"!==e.id))            // isWebApp / isMAC
E          && (S = S.filter(e => "LaunchAimLab"!==e.id))         // isMAC
n[FirmwareUpdate].hasIconMorePovot = hasFWUpdate                  // 固件页红点
n[Promotion].hasIconMorePovot      = !hasEverEnterPromotion        // 推广页红点
```

切换 Tab 时 `onEnterPage(r)` 会**按页触发网络请求**（`I@805.9k` 起）：

| Tab | 进页动作 | 证据 |
|---|---|---|
| FirmwareUpdate | `getDeviceInfo({isFWVersionOnly:true})` → GET `/deviceInfo` | `I@805.9k` |
| Performance | 若 `caps.performance.hasSeamlessDPI` → `getSystemInfo()` → GET `/systemInfo` | 同上 |
| Promotion | 首次进入 → `removePromotionHint()` | 同上 |
| 其它 | 仅 `dispatch(setCurrentPage(r,true))` | `I@806.2k` |

---

## 2. 状态管理

### 2.1 Redux 装配（真 redux，非 Context）

- 打包了 redux 4.x（压缩错误串 `"Minified Redux error #"` 在 `I@787873`，`@@redux/INIT` 在 `I@788460`）。
- 创建 store（`I@900850` 起）：

```js
window.actionsCollection = e;                 // 各动作模块
var t = window.__REDUX_DEVTOOLS_EXTENSION_COMPOSE__ || ht;
ea = pt(Vi, t(...))                            // pt = createStore，Vi = 根 reducer
```

- 没有 `configureStore`/`createSlice`，是**手写 switch 根 reducer**（`case` 数 **89**，`I@8.9k` 区段统计），例如：

```js
case ui.default.UPDATE_SYSTEM_INFO: return Ki(Ki({},e),{},{systemInfo:Ki({},t.data)});
case ui.default.SET_JOYSTICK_MODE:  return Ki(Ki({},e),{},{joyStickMode:t.status});
```

### 2.2 10 个 state 切片

`I@885000–900400` 扫描得到切片名：`language`、`common`、`globalProps`、`buttons`、`lighting`、`performance`、`calibration`、`dialSetting`、`power`、`settings`。

各切片初值（`I@879400` 起）：

```js
Zi = {deviceType:null, modelName:null, modelNumber:null, dongleSN:void 0, deviceSN:void 0,
      multiModelNumber:[], fwVersion:null, nationCode:"0", deviceMode:"0",
      profileList:[{id:"1",…},{id:"2",…},{id:"3",…}], swProfileList:[{id:"UUID",…}]}
ci = {currentLanguage:"English", mappingTable:null, langText:function(){return""}}          // I@872.3k
Si = {buttonList:{}, joystick:{mode:"Digital",direction:""}, gesture:{}}                      // I@874.8k
```

### 2.3 动作模块按 deviceType 分组

`I@226744` 定义 `ACTION_GROUP`：

```js
m = {"1":{buttons,calibration,file,index,dial,lighting,performance,power,routers,websocket}}
```

启动时 `Object.keys(ACTION_GROUP[deviceType])` 逐个 `n(444)("./"+name)` **顺序**加载（`I@899.9k`），全部挂到 `window.actionsCollection`；页面里直接取 `window.actionsCollection.index` / `.buttons` / `.lighting`（`I@293.9k`、`I@302.7k`）。→ 本代码库最特殊的架构点：**action creator 通过全局对象跨模块共享**，而非 import。

同时 `window.globalVariable = {currentApp:"Device Page"}`（`I@899.8k`），作为日志 tag。

### 2.4 设备配置对象的关键字段

以 `D` 与 reducer/组件为准：

| 切片 | 关键字段 |
|---|---|
| `buttons` | `buttonList{"1".."7":{buttonFunction,key_1..key_4,key2Value..key4Value,layout}}`、`joystick{mode,direction}`、`gesture` |
| `performance` | `dpi1..dpi4`（**存档位索引字符串**，非 DPI 值）、`dpiNumber`、`currentDPI`、`pollingRate_Booster/_Common`、`angleSnapping`、`angleTuning`、`accelerate/decelerate`、`color{dpi1:"r,g,b"}`、`dpi1XY..dpi4XY` |
| `lighting` | `switchStatus`、`mouse{allAreaFlag,areaKey:"3",area:[{areaKey,effectID,colorType,brightness,speed,direction,random,pattern{colors,singleColor}}]}`、`customPattern`、`documentColors`、`auraSyncBrightness` |
| `calibration` | `calibrationFunction/Material/Height/Processing/MaterialUI` |
| `power` | `timeToSleep`、`lowPowerToShine`、`powerStatus`、`chargingStatus` |
| `settings` | deviceType/modelNumber/deviceSN/dongleSN、`profileList`、`swProfileList`、`selectedProfile`、`projectedProfile`、`isPreviewMode`、`isWDLSupported`、`libVersion`、`url`、`alertSocketServer`、`logSocketServer` |

`D` 结构（JSON.parse 实测）：`buttons`(1 组) / `lighting`(5 组，profileID 1..5) / `performance` / `calibration` / `power`；默认灯效 `effectID ∈ {0,1,2,4,6,241}`，`241` = AURA（模块常量 `AURA:"241"`）。

---

## 3. 页面 → 服务调用（全部消息构造点）

### 3.1 三条通道

`I@115.9k` 算出全部基址（host 取 `window.location.host`，否则 `process.argv` 的 `host=`，默认 `127.0.0.1:1042`）：

```js
baseUrl = "http://"+host; ws = "ws://"+host
url    = baseUrl + "/api/" + apiVersion + "/" + url      // url = "type/1/model/6871"
libUrl = baseUrl + "/lib/" + libVersion + "/" + url
dataCollectUrl = baseUrl + "/lib/" + libVersion + "/" + url + "/collect"
```

| 通道 | 说明 | 证据 |
|---|---|---|
| HTTP REST | 业务读写，PUT/GET/PATCH/DELETE，JSON 体 | 见 3.2 |
| WS `alertSocketServer`(ws://127.0.0.1:9013) | **deviceService 通道**，消息体是 **XML**（`{xml:{command,…}}` 经 xml2js Builder 序列化） | `I@293873`、`I@301844` |
| WS `settings.ws`(ws://host) | `role=home`（插拔/跨页广播）、`role=devicePage`（页面事件） | `I@296093`、`I@298247` |
| WS `logSocketServer`(ws://127.0.0.1:9014) | `role=devicePageLogger`（页面日志） | `I@204723` |

```js
A+"?role=home&deviceType="+d+"&pid="+modelNumber                              // I@296093
composeURL(A+"?role=devicePage&deviceType=…&pid=…",{dongleSN,deviceSN},true)   // I@298247
composeURL(e+"?role=devicePageLogger&deviceType=…&pid=…",{dongleSN},true)      // I@204723
```

### 3.2 HTTP 端点清单（6871/index.js 实测）

| 端点（相对 `settings.url`） | 方法 | 触发 | 证据 |
|---|---|---|---|
| `/initialize/devicePage` | GET | 启动初始化；参数 `dongleSN,deviceSN,isElectron` | `I@344213` |
| `/initialize/alertPage` | GET | 告警页初始化 | `I@347316` |
| `/deviceInfo` | GET | 电量/固件/rf/usb/booster 版本、currentDPI | `I@335452` |
| `/systemInfo` | GET | 显示器列表（seamlessDPI 用） | `I@336837` |
| `/buttons` | PUT | 按键保存（5 处） | `I@245020` 等 |
| `/performance` | PUT | DPI/轮询/角度保存（6 个动作） | `I@379k–385k` |
| `/lighting` | PUT | 灯效保存（4 处） | `I@362626` 等 |
| `/calibration` | PUT | 表面校准 | `I@262810` |
| `/power` | PUT | 省电 | `I@388470` |
| `/dial` | PUT | 拨盘，`data:{uuid,jsonData:"<JSON 字符串>"}` | `I@311996` |
| `/profile/<id>` | GET | 读单 profile | `I@335.4k` |
| `/profile` | PATCH | 覆盖写入 profile（timeout 60s） | `I@325768` |
| `/reset` | GET / DELETE | 复位 / 删除 profile | `I@330302` / `I@354997` |
| `/update` | PUT | 固件升级提交 | `I@356082` |
| `/replugin` | GET | 重新枚举设备 | `I@356561` |
| `/joystick` | PUT | 摇杆模式 | `I@355466` |
| `/demoMode` | GET/PUT | 演示模式 | `I@336369`/`I@357153` |
| `/WDLControl`、`/WLDSyncStatus` | PUT / GET | Windows 动态光 | `I@358941`/`I@357930` |
| `/file` | POST | **写文件**（body 为 XML，function key = 11 WRITE_FILE） | `I@316105`、`I@316436` |
| `/file/<name>` | GET / DELETE | 读/删文件 | `I@316.4k` 之后 |
| `/event/power` | PUT | 电源事件 | `I@389576` |
| `/lib/1/devices`、`/lib/1/language`、`/lib/1/category/gear/install/<x>` | GET | 设备列表、系统语言、Crate 版本 | `I@276151`、`I@276574`、`I@277303` |
| `/collect` | POST | 数据采集 | `dataCollectUrl` |

写文件的 XML 生成（`I@316436`）：

```js
m={root:{device_type:{$:{key:a},device:{$:{key:s},function:{$:{key:S.WRITE_FILE},
   file_name:e,file_data:btoa(encodeURIComponent(…))}}}}
f.Z.sendRequest(d,y,{method:"post",contentFormat:"json",encodeFormData:!1})
```

两条 Profile 保存路径（软件配置 vs 板载配置）：

```js
saveGlobalConfig:    "config"+deviceSN+".xml"                     // I@319.6k，全局状态
saveFWProfileConfig: "fp_"+deviceSN+"_"+profileID+"_config.xml"   // I@318744，板载
saveSWProfileConfig: 同构，sw_ 前缀（导出表 I@314792）
```

### 3.3 WS 命令（XML，页面主动发）

页面**只构造 10 类业务 XML 命令**（`command:"…"` 字面量仅 12 个）：

| command | 字段 | 超时 | 证据 |
|---|---|---|---|
| `ui_pid` / `model_ui_pid` | `device, dongleSN` | — | `I@301844` |
| `GetMacroList` | `device, dongleSN` | 3s | `I@308217` |
| `GetAuraSyncState` | `device, dongleSN` | 60s | `I@308711` |
| `GetRelScenarioProfileList` | `device, dongleSN, profile, device_type` | 6s | `I@309838` |
| `GetRelScenarioProfileIcon` | 同上 | — | `I@310419` |
| `GetWindowsLightingState` | `device` | — | `I@310863` |
| `SetDeviceProfile` | 回显 `deviceProfileContent` + `result:true/false` | — | `I@294455`/`I@294612` |
| `DeviceProfileChange` | `device, device_type, profile, dongleSN, old_device_profile` | — | `I@306674` |
| `UpdateDeviceProfile` | `device, device_type, dongleSN, old_device_profile, new_device_profile` | — | `I@306994` |
| `ShowToast` | `device, dongleSN, command_type, model_name, text, image, DPI` | — | `I@307530` |
| `writeLog`/`WriteLog` | `deviceType, level, log` | — | `I@205009`、`I@306122` |
| `broadcastEvent` | `target, msg`（**走 home WS 的纯 JSON**） | — | `I@295047` |

请求体统一构造（`I@293873`）：

```js
M = function(e,t){ var n=D({},e.xml); Object.keys(t).forEach(k=>n[k]=t[k]); return n }
a = new Builder({headless:true}).buildObject(i);  R.send(a)
```

### 3.4 「用户操作 → 消息 → 字段」总表

| 用户操作 | 消息（通道/方法/路径 或 command） | 关键字段 |
|---|---|---|
| 打开设备页 | WS 9013 XML `ui_pid`/`model_ui_pid` | `device`, `dongleSN` |
| 页面初始化 | HTTP GET `/initialize/devicePage` | `dongleSN`, `deviceSN`, `isElectron` |
| 切 Tab | Redux `SET_CURRENT_PAGE`（不联网），部分页附带 GET | 见 1.3 |
| 改 DPI / 轮询 / 角度 / 加减速 | PUT `/performance?deviceSN=<sn>` | `{profileID, data:{dpi1..dpi4, dpiNumber, currentDPI, pollingRate, pollingRate_Booster/_Common, angleSnapping, angleTuning, accelerate, color{dpiN:"r,g,b"}}, isPreview}` |
| 切换当前 DPI 档 | 同上 PUT + WS `ShowToast` | `currentDPI`；toast `{text:"DPI n"}` |
| 增减 DPI 通道 | PUT `/performance` | `dpiNumber` 变化、空档置 `"-1"` |
| 按键功能/宏绑定 | PUT `/buttons?deviceSN=<sn>` | `{profileID, data:<buttonList{id:{buttonFunction,key_1..key_4}}>, isPreview}` |
| 改灯效/亮度/颜色 | PUT `/lighting?deviceSN=<sn>` | `{profileID, data:{switchStatus, mouse:{areaKey:"3", area:[…]}}, isPreview}` |
| 表面校准 | PUT `/calibration?deviceSN=<sn>` | `{profileID, data:{calibrationFunction, calibrationMaterial, calibrationHeight, calibrationProcessing, calibrationMaterialUI}, isPreview}` |
| 省电设置 | PUT `/power?deviceSN=<sn>` | `{profileID, data:{timeToSleep, lowPowerToShine,…}, isPreview}` |
| 拨盘设置 | PUT `/dial?deviceSN=<sn>` | `{profileID, data:{uuid, jsonData:"<JSON 字符串>"}}` |
| 保存到板载 | 本地生成 `fp_<SN>_<profileID>_config.xml` → POST `/file`（function 11） | `{button, joystick, gesture, performance, lighting{switchStatus,mouse,customPattern}, calibration, power, dial}` |
| 保存全局配置 | `config<SN>.xml` → POST `/file` | `{deviceSN, dongleSN, profileList, swProfileList, currProfile, hasEverSyncAllProfile, hasMacroDeleted, hasMacroChanged, isHPC, lighting{…}}` |
| 读宏列表 | WS XML `GetMacroList` | `device`, `dongleSN` |
| 查 Aura 同步态 | WS XML `GetAuraSyncState` | `device`, `dongleSN` |
| 场景 profile | WS XML `GetRelScenarioProfileList/Icon` | `profile`, `device_type` |
| WDL 状态 | WS XML `GetWindowsLightingState` + HTTP `/WDLControl` | `device` |
| 固件升级 | 主进程 frameworkAPI（页面不发升级数据） | `checkFWUpdate`（未验证调用点） |
| 跨页通知 | WS home JSON `broadcastEvent` | `{command,target,msg}` |

> 关键实现事实：**页面从不写 SDK function key**。业务数据全部走 HTTP JSON（`/performance` 等），由 `S` 转 XML 后调 SDK（04/06 章）。页面自建 XML 的只有 3.3 表里的 10 条查询/回显命令。

### 3.5 页面与 service.js 的协议同源

`S` 与页面共用同一套协议常量（同一份源码被编译进两个 bundle）：function key 表 `KEY_CHANGE:"0"` 在 `S@396057`，与 `I` 的模块 518 完全一致；事件码 `MOUSE_KEYEVENT:"001"`…、灯区 `LOGO/SCROLL_WHEEL/BASE/GLOBAL/SIDE/DPI/FRONT/DOCK`、效果 `STATIC/BREATHING/…/AURA:"241"` 也都同源。
`S` 侧主动收/发的 command（`command:"…"` 去重后 17 类）：`GetDeviceProfileList`、`GetDeviceProfile`、`GetDeviceDescription`、`IsNewDevice`、`ConnectionOpen`、`pid`、`SetDeviceProfile`、`DeviceProfileChange`、`GetDeviceLightingControl`、`AuraSync`、`UnAuraSync`、`GetAuraSyncState`、`GetMacroList`、`ShowToast`、`UpdateWindowsLightingState`、`broadcastEvent`、`writeLog`（`S@464672` 起）。→ **页面只发查询/回显，实际设备读写命令由 `S` 收**（与 06 章 §6.2 矩阵一致，细则见 04 章）。

### 3.6 设备 → 页面（WS 9013 收包）

`R.onmessage`（`I@301844` 之后）按 `command` 分派，收包是**纯 JSON**，并先用 `error` 与 `device` 匹配过滤：

| 收到的 command | 页面动作 |
|---|---|
| `SetDeviceProfile` | `U({deviceProfileContent,isSetDeviceProfile:true})` → 切 profile + 合并数据 + 回显 |
| `DeviceProfileChange` / `UpdateDeviceProfile` | 通知设备侧配置变化 |
| `GetMacroList` | `setMacro([{id,name,filePath,shiftMode}])` |
| `GetAuraSyncState` | `updateAuraSyncStatus(bool)` |
| `AuraSync` / `UnAuraSync` | 读 `config.xml` → `syncConfigSetting` |
| `MacroNew/Update/Rename/Delete` | 更新 macroList |
| `ShowToast` | 弹 Toast |

---

## 4. 表单校验与边界（前端如何限制）

全部来自 `C` + `B606` 的计算式；**前端不硬编码范围，范围是数据**。

| 项 | 规则 | 证据 |
|---|---|---|
| DPI 范围 | `start:100, end:36000, step:50`，4 档默认 `400/800/1600/3200`，`dpiAmount:"4"`，`indexShift:1` | `C.performance.items[dpi1..4]` |
| DPI 存取 | **存档位索引**：`idx = Math.ceil((value-start)/step) + indexShift`；反算 `value = start + (idx-shift)*step`；扩展档用 `additionItems.find(o=>o.value>=v && v>end)` | `B606@92384`、`B606@92280` |
| DPI 通道数 | 最少 2 档：`isDeletable: N && t>2`；删除时 `dpiN="-1"` 并前移，`dpiNumber-1`，`currentDPI` 越界减 1 | `B606@93766`；`I@382886` |
| 新增通道 | 仅当 `dpiNumber <= dpiAmount` 才写 `dpi{N+1}` | `I@384062` |
| 轮询率(booster) | 7 档 `125/250/500/1000/2000/4000/8000`，默认 `"6"`(8000)，带 `notice`+`replacement:"ROG Omni"` | `C.performance.items[pollingRate_Booster]` |
| 轮询率(common) | 4 档 `125/250/500/1000`，默认 `"3"` | `C.…[pollingRate_Common]` |
| 轮询率取值校验 | `options.some(o=>o.id===value) ? value : default`（非法值回落默认，不弹错） | `B606@96614`（组件 Ut） |
| 有线/无线互斥 | booster 与 common **互斥渲染** | `B606@106300–107100` |
| 角度微调 | 7 档 `-20/-10/-5/0/5/10/20`，默认 `"3"`(0) | `C.…[angleTuning]` |
| 角度吸附 | `off/on`，默认 `"0"` | `C.…[angleSnapping]` |
| 亮度 | 0–4 档 → `0%/25%/50%/75%/100%`；`highestBrightnessValue:"4"` | `C.lighting.effectList[*].items[brightness]`；`I@318744` |
| 颜色 | `color.dpiN = "r,g,b"` 字符串；`rgbToHex/hexToRgb` 互转；非法色回落默认色表 `Zt[o]` | `B606@92560`、`B606@93120` |
| RapidFire 间隔 | `intervalTime:{start:2,end:1000,step:2}` | `C.buttons.keyAssignFunctionList[id=10]` |
| Profile 数量 | `profileCount:5`；`profileHotKey.MainKey:["201".."205"]`；键码表含 `Profile0..Profile5` | `C.profileCount`、`C.profileHotKey`、`I@18.0k` |
| 电量 | `isFiveStages:true`、`isFuzzyPower:true` → `powerStatus=10*Math.ceil(x/10)`（模糊到 10%） | `C.power`、`I@295k` 后 |
| 校准 | `hasManualCalibration`、`hasPresetSurfaces`、`noCalibrationCanStillSetLOD`；`mousePadList` 从 `255`(默认) 起；`calibrationHeight` 默认 `"0"` | `C.calibration`、`D.calibration` |
| 固件版本项 | `fwVersion / usbVersion / rfVersion / boosterVersion` 四项按 caps 列表渲染 | `C.firmwareUpdate.versions` |
| deviceSN 合法性 | `p=/[^0]/`（含非 0 字符才算有效），否则不 dispatch | `I@899.7k` |

---

## 5. 键位映射 UI

### 5.1 7 键的 id 与显示名（caps 为唯一真源）

`C.buttons.buttonList` 只有两个分组：`top`（顶视 5 键）+ `left`（侧视 2 键）= **7 键**；组标题 `row_259`=Top、`row_260`=Left。

| 设备 id | 分组 | defaultKey | langRef | 英文显示名（L） |
|---|---|---|---|---|
| 1 | top | `LeftButton` | `row_46` | Left button |
| 2 | top | `RightButton` | `row_47` | Right button |
| 3 | top | `ScrollClick` | `row_11` | Scroll click |
| 4 | top | `ScrollUp` | `row_8` | Scroll up |
| 5 | top | `ScrollDown` | `row_9` | Scroll down |
| 6 | left | `LeftForward` | `row_13` | Forward |
| 7 | left | `LeftBackward` | `row_12` | Backward |

侧键额外带 `isSupportRapidFire:true`（`C`），只有 6/7 支持连发。

### 5.2 keyDefinitions 的用法

- 数据源：`resources/src/_ref/keyDefinitions/<locale>[_mac].json`（22 个），**构建期内联**进 `I` 的 settings 模块（`I@17789` 起 `keyDefinitions:{"de-DE":[…],"en-US":[…],…}`）；运行时从 `n(1505).Z.keyDefinitions[locale]` 取。
- 结构：**29 行 × 8 列的字符串矩阵**（`en-US.json` 首行 `["Esc","<反引号>","Tab","Caps Lock","L-Shift","L-Ctrl","1","2"]`），覆盖主键区/小键盘/鼠标键/摇杆键/Profile 键（末几行含 `LeftButton/RightButton/ScrollClick/ScrollUp/ScrollDown/DPI1/DPI2/DISABLE`、`Profile0..Profile5`）。
- 读取算法（`I@127878`，`keyboardGetKeyName(keyCode, table)`）：

  ```js
  n = Number(e).toString(16);  pad 到 4 位 → "0xyz"
  r = parseInt(n.substr(0,2),16);          // 列
  return t[parseInt(n.substr(2,2),16)][r]; // 行 = 低字节
  ```

- 反查：`keyboardGetKeyNumber(name, table)`（保存时显示名 → 码值）。
- 布局切换：按键带 `layout` 字段，取值 `<locale>` 或 `<locale>_mac`：`D[(layout||"en-US")+(isMAC?"_mac":"")]`（`B606@~26.3k`）。

### 5.3 键盘图（按键分配图形界面）

- 视觉键盘键位表来自 `keyElementMapping/<layout|Mac>/tableN.json`：`B606` 模块 2005 是上下文映射（`"./keyElementMapping/Mac/table1.json":1020` … 共 21 张表），实体组件在 `B989` 模块 3551（引用修饰键表 `["Alt","Ctrl","Shift","Win","Fn"…]`）。
- 组件用 `keyboardGetMappingKey(col,row,altKey,layout)` 在 `layout` 表里定位被点击的键（`I@128.0k`）。
- 组合键输入框走 `comboKeyInputBox`（caps `keyAssignFunctionList[id=1]`），保存为 `key_1="<键码>"`；宏/剪贴板类型时 `key_1` 是 `encodeURIComponent` 过的字符串（`B606@~26.5k` 有 `decodeURIComponent(m.key_1)`）。

### 5.4 主功能下拉（keyAssignFunctionList）

`C.buttons.keyAssignFunctionList` 每项 = 一个功能类别，`subFunction.type` 决定右侧控件：

| id | icon | langRef | subFunction.type | 说明 |
|---|---|---|---|---|
| 0 | optionIcon1 | `row_6` | `dropdownList` | 鼠标功能（LeftButton/RightButton/…/DPI1/DPI2/DoubleClick） |
| 10 | optionIcon14 | `row_479` | `rapidFire` | 连发，interval 2–1000 步 2 |
| 1 | optionIcon2 | `row_7` | `comboKeyInputBox` | 键盘功能（组合键录入） |
| 4 | optionIcon3 | `row_15` | `dropdownList` | 宏（默认 `-1` disabled，实际列表来自 `GetMacroList`） |
| 3 | optionIcon5 | `row_24` | `dropdownList` | 多媒体（VolumeUp/Down/Mute/PlayPause/Prev/Next/Stop/MicMute） |
| 2 | optionIcon4 | `row_17` | `dropdownList` | Windows 快捷键（0–5） |
| 11 | optionIcon15 | `row_634` | `message` | 提示项 |

按钮状态由 `B606@~26k` 的 useEffect 计算：`{keyName, displayString, buttonFunction, isAssigned, isDisabledForRapidFire}`；`isAssigned` 用「当前键名 vs profile defaultKey 归一化比较」（`RightBackward→LeftBackward`、`RightForward→LeftForward` 后比对，`B606@~27k`）。

---

## 6. 国际化

### 6.1 数据形态

语言包是**扁平 XML**，每个文案一个 `row_N` 元素：

```xml
<root><row_2>Buttons</row_2><row_3>Performance</row_3>…</root>
```

`L` 实测：`row_2`=Buttons、`row_3`=Performance、`row_4`=Lighting、`row_5`=Calibration、`row_207`=Power、`row_77`=Firmware Update、`row_46`=Left button…。共 **24 个语言 XML** 在 `resources\` 下（如 `English-68efc.xml`）；另外 `resources\node_modules\ac-components\src\_ref\language\*.xml` 还残留同一套 24 份**未打包的源文件**（构建产物泄漏），从中可见共用组件库名为 `ac-components`。

### 6.2 引用方式：langRef → window.langText

- caps/组件里不写文案，只写 `langRef:"row_N"`；
- 页面统一 `window.langText("row_N")`（数百处，如 `B606@83261`、`B606@123500` 的 `window.langText("row_457")`）；
- `window.langText` 由 reducer 注入（`I@881605`）：

```js
case ui.default.SET_LANGUAGE: var r=t.data, o=n(r); window.langText=o; … {mappingTable:r, langText:o}
case ui.default.CHANGE_LANGUAGE: … {currentLanguage:t.data}
case ui.default.EXTEND_LANGUAGE: … 合并 externalLanguageData 后重建
```

- 非 `row_N` 的 langRef 直接当键返回：Promotion 的 `langRef:"Armoury Crate Gear"`（`C` 原文即英文），说明 `langText(key)` 找不到时回退 key 自身。

### 6.3 语言包加载路径（两条）

| 模式 | 路径 | 证据 |
|---|---|---|
| UWP/Electron 内嵌 | UWP 桥 `getCurrentLanguage` → 内部固定 `c.Z.loadLanguage("English")`，返回 `{language:"English", languageTable}`；收到 `LANGUAGE` 事件时按系统 locale 重选 | `I@210047`、`I@210191`、`I@214341` |
| Web（isWebApp/isElectron） | GET `/lib/1/language` 取系统语言 → `zh`/`hant` 映射到 Simplified/Traditional Chinese → `require.context(8616)("./<Lang>.xml")` 拿**资源 URL** → GET 该 XML → `SET_LANGUAGE` | `I@276574`、`I@277610` |

`8616` 上下文（`I@126`）把 23 个 XML 映射到模块 id（`"./English.xml":7190`）；XML 模块 `module.exports = n.p + "resources/English-68efc.xml"`（同类实现见 `I@698470` 附近）——语言包以静态资源随页面发布，运行时按语言名再请求一次。`isZhCN` 由 `updateIsZhCN("Simplified Chinese"===o)` 单独标记（`I@278.2k`）。

---

## 7. 页面 ↔ 主进程握手、sessionKey、错误码

### 7.1 启动参数（URL query）

壳组件在路由拦截器里解析（`I@899.6k`）：

```js
i=new URL(r.url).searchParams;
sid, logoHidden, mode("icafe"|"bootloaderMode"), isElectron("1"), deviceSN, dongleSN, isMAC
p=/[^0]/  →  deviceSN/dongleSN 必须含非 0 字符才采用
```

对应 dispatch：`updateSID / updateHeaderLogo / setICafeMode / setIsWebApp / updateDeviceSN / updateDongleSN / setBootloaderMode / setIsMAC / updateOSVersion`。

### 7.2 两条握手

1. **deviceService 握手**（XML，`I@301844`）：连上 9013 后立刻发

   ```js
   {xml:{command: isWebApp?"model_ui_pid":"ui_pid", device: customizePID||modelNumber, dongleSN}}
   ```

2. **home 握手**（纯 JSON，`I@296093`）：`?role=home&deviceType=…&pid=…`，收 `DEVICE_PLUGGED/DEVICE_UNPLUGGED` 与设备列表；`I.onclose` 置空并 3s 后重连（`I@297079`）。

日志连接独立：`?role=devicePageLogger`（9014），连上先发 `{command:"writeLog",deviceType,level:4,log:"***** [websocket] Connected *****"}`（`I@204987`）——页面日志通道的自检探针。

### 7.3 sessionKey 的真实语义

`I@308217` 起每个 XML 查询都做同一套动作：

```js
u = g.Z.generateUUID();
L.push({sessionKey:u, cmd:"GetMacroList", resolve:t, reject:r});
R.send(a); window.setTimeout(()=>B("GetMacroList"), 3000)
```

- `generateUUID()`（`I@125938`）：`Date.now()+performance.now()` 取模的 v4 形态 → **可预测、无安全熵源**（仅 `Math.random` 兜底），不适合做安全标识。
- **回包匹配用 `cmd` 而非 `sessionKey`**：`B(e,t,n)` 里 `L.find(x=>x.cmd===e)`（`I@294464`）→ 同一 command 只能有一个在飞请求；`sessionKey` 被记录但未参与匹配（实现级约束：并发同命令会串包）。
- 超时按命令不同：`GetMacroList` 3s、`GetRelScenarioProfileList` 6s、`GetAuraSyncState` 60s（超时直接 resolve 默认值，不 reject）。

### 7.4 deviceSN / dongleSN 的传递链

| 环节 | 传递方式 |
|---|---|
| 主进程 → 页面 | URL query（`?deviceSN=&dongleSN=`） |
| 页面 → HTTP | 每请求 query：`composeURL(url,{deviceSN, dongleSN})`（`I@115.9k`） |
| 页面 → WS | URL query（`&deviceSN=&dongleSN=`） |
| 页面 → 配置文件 | 文件名内嵌：`config<SN>.xml`、`fp_<SN>_<id>_config.xml` |
| 设备 → 页面 | WS 回包 `deviceSN`，且 `/[^0]/.test(s)` 才 `updateDeviceSN`（`I@298.3k`） |

### 7.5 错误码展示

- 回包过滤：`if(!a && i===(customizePID||modelNumber))`（`a=error`，`i=device`）→ 只处理无错且设备匹配的消息（`I@301844`）。
- 初始化失败默认码：`errorCode ?? 2006 (INIT_FAILED)`（`I@298.3k`）。
- 统一弹窗（`I@805.7k`）：

```js
setDialog({type:"alert", title:langText("row_457"),      // "Operation Failed"
           content:langText("row_458"),                  // "Sorry, the last operation failed…"
           errorCode: n ? ""+n : null})
```

- 错误码常量在页面里有完整副本（`I@223.9k` 模块 9587 + 518），与 06 章 §6.3 分类一致：1000–1006(SDK)、1010–1032(资源/请求)、2005/2006/2008–2012、3001–3005、4001–4005、5001–5003、9001/9002(SSL 文件)。**页面只显示数字串，未找到 code→row_N 映射**（UI 是否只显示数字未验证）。

---

## 8. 四套页面的实现差异

| 维度 | 6871（2.4G 设备页） | p520（产品页） | 6862（接收器页） | omni（接收器主页） |
|---|---|---|---|---|
| 仓库 | AC_Frontend_Mouse | AC_Frontend_Mouse（同一壳） | AC_Frontend_UnifyDongle | AC_Frontend_UnifyDongle |
| deviceType | 1 | 1 | 21 | 21 |
| index.js | 903,964 B | 675,883 B | 568,114 B | 406,898 B |
| 分包 hash | `e147c` | `3ca91` | `5b995` / 无 hash 命名 | `462d9/794fc` |
| 设备页入口 | 模块 3672 → chunk `989+606` | 模块 5053(chunk 9) + 7 个模型入口 | 模块 9936 → chunk `936` | 模块 393 → chunk `9,457,338` |
| 模型入口 | `./6871` | `./home ./md300 ./p519 ./p520 ./p713 ./p714 ./p716 ./p718` | `./6862` | `./omni` |
| caps 功能页 | 7 个（Buttons…FirmwareUpdate） | 空 functionList（产品/连接引导） | `Dongle`,`FirmwareUpdate` | 空（`isWebApp:true`） |
| service.js | 有（621,724 B） | 有 | 无 | 无 |
| WS 用法 | 9013 XML 命令 + home/devicePage JSON | 同 6871（**业务代码整份重复**） | 纯 JSON，自有 cmd 事件码 | 纯 JSON |
| 业务端点 | 全套 `/buttons /performance /lighting /calibration /power /dial /profile /file` | **同一套端点同样存在** | 无业务端点，只用 `/collect`、`/externalFiles/common/scenarioProfile/` | 只用 `/collect` |
| 特有常量 | Profile/RapidFire/Aura | `connectPriorityList:["6873","6871"]`、`bootloaderList:["6863"]`、`connectList`(RF/BT)、`?session=` | 配对：`PAIR_UNPAIR`、`PAIR_STATUS_CHANGE:"60000"/"60001"`、`DEVICE_PLUGGED:"30001"`、`DONGLE_SN_NOT_MAPPING_AND_DEVICE_SN_ZERO:"P-1000"`、配对类型 `mouse:"1"/keyboard:"2"` | 同 6862 骨架，配对 UI 简化 |

补充证据：

- p520 模型上下文（`p520/index.js`）：`"./p520":[1342,9,990,250]`、`"./home":[4495,9,990,690]`、`"./md300":[557,9,990,364]`、`"./p713":[1566,9,990,425]`、`"./p714":[338,9,990,993]`、`"./p716":[2665,9,990,584]`、`"./p718":[6143,9,990,802]` → **p520 是「多产品合页」**，按 `multiModelNumber` 装载模型包。
- 6862 的 WS 封装是独立模块 3408：`y.send(JSON.stringify(e))`、`url = ws + "?role=devicePage&pid=" + modelNumber + "&deviceType=" + deviceType`，收包按 `cmd` 分发 `PAIR_STATUS_CHANGE`（`6862/index.js@283080` 之后）→ 与 6871 的 XML 通道**协议不同**。
- omni 入口 `[393,9,457,338]`，`./omni/caps` 在 chunk 28、`./omni/style` 在 chunk 676。

---

## 9. 页面功能页 ↔ 消息 ↔ 后端能力 总表

| 功能页 | 页面入口/组件 | 页面发出的消息 | 服务端命令 → SDK function key（引自 04/06） |
|---|---|---|---|
| Buttons | `B606` 组件 `Nt`@83261；键表 `B989` 模块 3551；模型弹窗 `B219`@7219 | PUT `/buttons`{profileID,data.buttonList,isPreview}；WS `GetMacroList`；`/file` 存 `fp_*_config.xml` | key 0 SetKeyChange / 10 Enumerate / 15 SetDail / 16 SetJoystickMode |
| Performance | `B606` `tn`@108167（DPI 滑杆 xt、轮询 Ut、角度 Wt/Vt/Jt） | PUT `/performance`；GET `/systemInfo`；WS `ShowToast` | key 1 SetPerformance；key 6 SetLoadData |
| Lighting | `B606` `Rn`@122592 | PUT `/lighting`；WS `GetAuraSyncState/AuraSync/UnAuraSync`；`/WDLControl` | key 2 SetEffect、7 SetSync、8 SetFrame、20 SetWDL |
| Calibration | `B606` `gn`@111625 | PUT `/calibration`；WS `GetRelScenarioProfileIcon` | key 3 SetCalibration |
| Power | `B606` `kn`@117562 | PUT `/power`；PUT `/event/power`；WS `ShowToast` | key 4 SetPower |
| Promotion | `B606` `_n`@119283 | 无业务消息；`removePromotionHint` | key 19 SetDemoMode / 13 SendNotification（推断） |
| FirmwareUpdate | `B606` `Pn`@122534 | GET `/deviceInfo`（仅版本）；`/update`；主进程 `checkFWUpdate` | key 17 SetFwUpdate |
| 顶栏 Profile | 壳（`Profile0..5`、`MainKey 201–205`） | PATCH `/profile`、GET `/profile/<id>`、DELETE `/reset` | key 5 SetProfile、18 SetDeviceReset |
| 初始化/握手 | 壳 `I@899.6k` | WS `ui_pid`/`model_ui_pid`；GET `/initialize/devicePage` | key 10 Enumerate、6 SetLoadData |
| 跨页/通知 | 壳 | WS home JSON `broadcastEvent`；WS 9013 XML `ShowToast` | key 13 SendNotification |

---

## 10. 未验证 / 待补

1. `/api/<apiVersion>` 实际版本由 `config.ini` 的 `APIVersion=2` 注入，未在运行时核对最终 URL 串。
2. 固件升级真实调用点落在主进程 `checkFWUpdate`（页面只发 `/update`），升级数据面未在页面层验证。
3. `sessionKey` 服务端是否回显并校验（页面只按 `cmd` 匹配，服务端行为未验证）。
4. `ShowToast` 的 `command_type/image/DPI` 取值域未穷举（只在 DPI 切换处见 `{text:"DPI n"}`）。
5. p520 为何内嵌整套设备页业务代码（是否复用 6871 渲染路径）属架构推断，未见运行时证据。

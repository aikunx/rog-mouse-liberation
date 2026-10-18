# 03 · ArmouryDevice 页面层逆向（View / app.asar / service.js）

> 目标设备：ROG STRIX IMPACT III WIRELESS（P520）。分析对象为只读副本，全程未执行任何 JS / exe，未修改安装目录。
> 图形界面宿主：Electron（asus_framework 4.2.2.8）。所有「依据」均指实际读取到的文件、字符串或配置键；推断处标注「推断」，无法确证处标注「未验证」。

## 0. 结论速览

| # | 结论 | 关键依据 |
|---|---|---|
| 1 | 4 个 PID 页面来自 **两套不同的前端仓库**：p520/6871 来自 AC_Frontend_Mouse，6862/omni 来自 AC_Frontend_UnifyDongle（MD5.ini 里的构建绝对路径 + 分包命名规则不同） | 各页 MD5.ini 的 FileName 前缀 |
| 2 | 每个页面都是「一个 `index.html` + 一个 webpack 单文件 `index.js` + 若干 `<id>-bundle-<hash>.js/css` 异步分包」 | View\*\index.html、分包清单 |
| 3 | p520 与 6871 额外带 `service.js`（621 KB / 621 KB 级），由主进程用电 **Electron utilityProcess.fork** 拉成独立进程，不是页面内模块 | app.asar → .webpack/main/index.js 的 `utilityProcess.fork` 片段 |
| 4 | 页面通过 **HTTP + WebSocket 双通道**访问本机框架服务：默认 `127.0.0.1:1042`（HTTPS 1043），WS 角色有 `home`/`devicePage`/`deviceService`/`devicePageLogger` | 页面 JS 字符串、主进程常量 `ports:{http:"1042",https:"1043"}` |
| 5 | 页面本体 **完全不接触 HID**：`WriteFile`/`Report ID`/`51 31`/`ExecuteFunction` 在 4 个 index.js 与 service.js 中出现次数均为 0 | 正则扫描计数 |
| 6 | 6873（蓝牙页）**已安装目录中没有**，只存在于安装包 `Device\p520\6873\6873` 内；p520 的 Setup.ini 已登记其卸载 GUID | `View` 目录列表、安装包目录、p520\Setup.ini |
| 7 | 设备的全部 UI 能力由一段 **caps 配置**（`resources\src\_ref\caps.json`，8.9 KB）驱动；同一份内容又以 JS 字面量内嵌在 service.js 里 | caps.json 19 个顶层键与 service.js 内嵌段逐一对应 |

## 1. 页面目录全景

页面根目录：[View](<C:/Program Files (x86)/ASUS/ArmouryDevice/View>)

| PID | 目录 | 递归文件数 | index.js | index.css | service.js | Setup.ini | DeviceType | ArmourySocketServer | 对应界面 |
|---|---|---|---|---|---|---|---|---|---|
| p520 | `View\p520` | 111 | 675,883 B | — | — | 有（版本 1.0.2.2） | 1 | true | 产品页：无线三模鼠标的「整机」入口，含 2.4G / 蓝牙连接引导 |
| 6871 | `View\6871` | 619 | 903,964 B | 66,943 B | 621,724 B | 无 | 1 | true | 2.4G（Dongle/RF）设备页：按键 / 性能 / 灯光 / 校准 / 电量 / 固件 |
| 6862 | `View\6862` | 123 | 568,114 B | 60,222 B | — | 无 | 21 | false | 接收器设备页（统一接收器自身） |
| omni | `View\omni` | 89 | 406,898 B | 3,372 B | — | 有（版本 1.0.0.2） | 21 | false | 接收器主页：配对管理 / 已配对设备列表 / 复位 |
| 6873 | 已安装目录**不存在** | — | — | — | 仅安装包内有 | 仅安装包内有 | 1 | true（安装包内 config.ini） | 蓝牙（Bluetooth）设备页 |

- 6873 证据：`Get-ChildItem C:\Program Files (x86)\ASUS -Recurse -Directory -Filter 6873` 返回空；安装包 `Device\p520\6873\6873` 内 27 个顶层文件、index.js 903,961 B、service.js 621,137 B，分包 hash 后缀为 `-fb271`（6871 为 `-e147c`）。
- p520 的 Setup.ini 已登记 6873 的卸载 GUID `{603cdb29-461b-42d5-98c3-58fd30e5f4df}`，与安装包 `Device\p520\6873\Setup.ini` 的 `GUID` 完全一致 → 推断：三模鼠标的蓝牙页面属于同一次安装的组件，本次环境只是未落到 `View` 目录（未验证原因）。
- 页面内还有两个二进制：`HomePage.exe`（5,278,568 B，p520/omni）与 `Shortcut.exe`（1,562,984 B，p520/omni）+ `shortcut.lnk`。它们只在带 Setup.ini 的「产品级」目录出现 → 推断为安装/快捷方式辅助程序，与运行时页面渲染无关（未执行，未验证）。

## 2. 四个页面逐一

### 2.1 p520（产品页）

| 项目 | 值 |
|---|---|
| index.html | 317 B，单挂载点，无 CSS 链接 |
| config.ini | 122 B，`[ArmourySDK]` 7 键 |
| MD5.ini | 18,661 B / 218 行，`Count=105` |
| Setup.ini | 874 B，`Name=p520`、`GUID={c0bfe97f-154d-4a56-a88c-984378f29ac8}`、`DisplayName=ROG STRIX IMPACT III WIRELESS`、`DisplayNameML=ROG 影刃 3 无线版`、`[HomePage] CMD = /lib/1/window/p520` |
| 分包 | 39 个 `<id>-bundle-3ca91.(js|css)`，后缀 `-3ca91` |

index.html 全文：

~~~~html
<!DOCTYPE html> <html lang="en"> <head> <meta charset="UTF-8"> <meta name="viewport" content="width=device-width,initial-scale=1"> <meta http-equiv="x-ua-compatible" content="ie=edge"> <title>Armoury Peripheral</title> </head> <body> <div id="root"></div> <script defer="defer" src="index.js"></script></body> </html>
~~~~

config.ini 全文：

~~~~ini
[ArmourySDK]
ArmourySocketServer = true
DeviceType = 1
APIVersion = 2
ServiceVersion = 1
UWPVersion = 1
ReloadS3 = 1
~~~~

p520 的 caps.json（产品页专用，带连接引导）全文：

~~~~json
{
 "device": "ROG STRIX IMPACT III WIRELESS",
 "deviceZH": "ROG 影刃 3 无线版",
 "guid": "c0bfe97f-154d-4a56-a88c-984378f29ac8",
 "productName": "Web AC - ROG STRIX IMPACT III WIRELESS",
 "deviceType": "1",
 "theme": "rog",
 "refModelNumber": "6871",
 "isWebApp": true,
 "connectPriorityList": [
  "6873",
  "6871"
 ],
 "bootloaderList": [
  "6863"
 ],
 "connectList": [
  {
   "pid": "6871",
   "connect": "row_1865",
   "image": "RF"
  },
  {
   "pid": "6873",
   "connect": "row_1866",
   "image": "BT"
  }
 ]
}
~~~~

要点：`refModelNumber:"6871"` 说明产品页把实际功能委派给 6871 页面；`connectPriorityList:["6873","6871"]` 表示优先蓝牙、其次 2.4G；`bootloaderList:["6863"]` 是升级模式下的页面 PID；`isWebApp:true`。

### 2.2 6871（2.4G 设备页，核心）

| 项目 | 值 |
|---|---|
| index.html | 357 B，含 `index.css` |
| config.ini | 142 B，额外 2 键 `DG=6871` / `BT=6873` |
| MD5.ini | 112,053 B / 1,238 行，`Count=616`（对应 619 个文件，差 3 个：config.ini / index.html / MD5.ini 自身不入清单） |
| Setup.ini | 无（随 p520 安装，不单独注册卸载项） |
| 分包 | `219/475/494/502/606/762/989` 七组，后缀 `-e147c` |
| 特殊文件 | `service.js`（621,724 B）+ `service.js.LICENSE.txt`（449 B） |

index.html 全文：

~~~~html
<!DOCTYPE html> <html lang="en"> <head> <meta charset="UTF-8"> <meta name="viewport" content="width=device-width,initial-scale=1"> <meta http-equiv="x-ua-compatible" content="ie=edge"> <title>Armoury Peripheral</title> <link href="index.css" rel="stylesheet"></head> <body> <div id="root"></div> <script defer="defer" src="index.js"></script></body> </html>
~~~~

config.ini 全文：

~~~~ini
[ArmourySDK]
ArmourySocketServer = true
DeviceType = 1
APIVersion = 2
ServiceVersion = 1
UWPVersion = 1
ReloadS3 = 1
DG = 6871
BT = 6873
~~~~

分包按体积分三档（webpack splitChunks 的典型分层）：

| 组件 | JS | CSS | 角色（推断） |
|---|---|---|---|
| `989-bundle-e147c` | 801,093 B | 95,145 B | 最大分包：灯光/宏/按键分配等重 UI 与第三方组件 |
| `606-bundle-e147c` | 135,535 B | 71,057 B | 次大：设备主视图（鼠标图 + 分区高亮） |
| `219-bundle-e147c` | 6,575 B | 22,597 B | 公共控件样式（ac-components） |
| `475 / 762 / 502` | 7,976 / 3,591 / 564 B | 7,503 B | 功能小组件（校准、固件、电量） |
| `494-bundle-e147c` | 117 B | 1 B | 空占位 chunk（动态 import 的桩） |

### 2.3 6862（接收器设备页）

| 项目 | 值 |
|---|---|
| index.html | 357 B，含 `index.css` |
| config.ini | 132 B，`ArmourySocketServer=false`、`DeviceType=21`、`ForceLoading=false` |
| MD5.ini | 22,310 B / 246 行，`Count=120` |
| Setup.ini | 无 |
| 分包 | `bundle-31c920dd490dffe316a3.js`（153 B）、`bundle-3eaa37bd05a66751623a.js`（34,119 B）、`bundle-67673ef258e351f69ef6.js`（1,465 B）、`936-bundle-5b995.css`（19,488 B） |

命名差异值得注意：6862/omni 用 **contenthash**（`bundle-<40hex>.js`），p520/6871 用 **chunkhash + id 前缀**（`<id>-bundle-<5hex>.js`）→ 两套 webpack 配置，印证来自两个不同前端工程。

config.ini 全文：

~~~~ini
[ArmourySDK]
ArmourySocketServer = false
DeviceType = 21
APIVersion = 2
ForceLoading = false
ServiceVersion = 1
UWPVersion = 1
~~~~

6862 的 caps.json（全文）：

~~~~json
{
 "device": "ROG OMNI RECEIVER",
 "guid": "c8296bac-8227-47bd-aae7-96f081d82527",
 "productName": "omni",
 "deviceType": "21",
 "theme": "rog",
 "pairDeviceWhiteList": {
  "mouse": {
   "id": "mouse",
   "type": "mouse",
   "name": "row_545",
   "deviceList": [
    {
     "pid": "6804",
     "modelName": "ROG HARPE ACE AIM LAB EDITION",
     "simplifiedName": "ROG 龙鳞 Ace AimLab 合作版",
     "productName": "p713"
    },
    {
     "pid": "6871",
     "modelName": "ROG STRIX IMPACT III WIRELESS",
     "simplifiedName": "ROG 影刃 3 无线版",
     "productName": "p520"
    }
   ]
  },
  "keyboard": {
   "id": "keyboard",
   "type": "keyboard",
   "name": "row_1601",
   "deviceList": [
    {
     "pid": "6832",
     "modelName": "ROG STRIX SCOPE II 96 WIRELESS",
     "simplifiedName": "ROG 游侠 2 98 无线版",
     "productName": "x901"
    },
    {
     "pid": "6918",
     "modelName": "ROG FALCHION RX LOW PROFILE",
     "simplifiedName": "ROG 魔导士 RX Low Profile",
     "productName": "m603"
    },
    {
     "pid": "6789",
     "modelName": "ROG AZOTH",
     "simplifiedName": "ROG 夜魔",
     "productName": "m701"
    }
   ]
  }
 },
 "deviceCountWhiteList": [
  {
   "id": "mouse",
   "type": "mouse",
   "name": "row_545",
   "count": 1
  },
  {
   "id": "keyboard",
   "type": "keyboard",
   "name": "row_1601",
   "count": 1
  }
 ],
 "functionList": [
  {
   "id": "Dongle",
   "text": "dongle",
   "langRef": "row_1597"
  },
  {
   "id": "FirmwareUpdate",
   "text": "Firmware Update",
   "langRef": "row_77"
  }
 ],
 "pidHintList": [
  {
   "pid": "6804",
   "link": "https://rog.asus.com/mice-mouse-pads/mice/wireless/rog-harpe-ace-aim-lab-edition-model/helpdesk_bios/"
  },
  {
   "pid": "6789",
   "link": "https://rog.asus.com/keyboards/keyboards/aura-rgb/rog-azoth-model/helpdesk_bios/"
  }
 ]
}
~~~~

这里能看到接收器页的真正职责：`pairDeviceWhiteList` 只放行 P520（6871）与 ROG HARPE ACE AIM LAB（6804）鼠标、以及 3 款键盘；`deviceCountWhiteList` 对每类设备限制 `count:1`；`pidHintList` 给出帮助页链接。

### 2.4 omni（接收器主页）

| 项目 | 值 |
|---|---|
| index.html | 357 B，含 `index.css`（3,372 B，页面极简） |
| config.ini | 132 B，与 6862 完全同构（DeviceType 21 / socketServer false） |
| MD5.ini | 16,326 B / 172 行，`Count=82` |
| Setup.ini | 760 B，`Version=1.0.0.2`、`Name=omni`、`GUID={7620362f-f7d2-46e7-bc98-7d932190450e}`、`DisplayNameML=ROG Omni 接收器`、`CMD = /lib/1/window/omni` |
| 分包 | `bundle-22c05b35ddcba62291c0.js` 338 B、`bundle-6b80f2e7635c7464c843.js` 32,268 B、`bundle-6dd79c233895fe3d4647.js` 402 B、`bundle-8b7b64bec4e537931a8c.js` 17,032 B、`338-bundle-462d9.css` 26,308 B、`676-bundle-794fc.css` 2,376 B |

omni 的 caps.json（全文，207 B，极简）：

~~~~json
{
 "device": "ROG OMNI RECEIVER",
 "guid": "7620362f-f7d2-46e7-bc98-7d932190450e",
 "productName": "Web AC - ROG OMNI RECEIVER",
 "modelNumber": "6862",
 "deviceType": "21",
 "theme": "rog",
 "isWebApp": true,
 "functionList": []
}
~~~~

config.ini 全文：

~~~~ini
[ArmourySDK]
ArmourySocketServer = false
DeviceType = 21
APIVersion = 2
ForceLoading = false
ServiceVersion = 1
UWPVersion = 1
~~~~

注意 `"productName":"Web AC - ROG OMNI RECEIVER"`、`"isWebApp":true`、`"functionList":[]` —— 该页面**没有任何功能页签**，纯配对/管理界面。

### 2.5 6873（蓝牙页，仅安装包）

`Device\p520\6873\Setup.ini` 全文关键字段：

~~~~ini
[Device]
Version=4.05.38
Name=6873
GUID={603cdb29-461b-42d5-98c3-58fd30e5f4df}
DisplayName=ROG STRIX IMPACT III WIRELESS (Bluetooth)
~~~~

其页面目录结构与 6871 完全同构（同样的 219/475/494/502/762/989 分包、同样的 `index.css` 66,943 B），仅 hash 后缀与 3 处导出的 chunk 不同（多了 `235/723/724/831`）。config.ini 与 6871 逐字节仅 CRLF/LF 差异。

## 3. index.html 的挂载点与脚本顺序

4 个页面共用同一模板，差异只在是否有 `index.css`：

| 页面 | 挂载点 | CSS | 脚本 |
|---|---|---|---|
| p520 | `<div id="root">` | 无 | `<script defer src="index.js">` |
| 6871 / 6862 / omni | `<div id="root">` | `index.css` | 同上 |

- 只有 **一个** 挂载点 `#root`，React 18 由 `index.js` 自行 `createRoot`（推断：index.js 中含 `react-dom` 与 `createRoot`，未逐符号验证）。
- `defer` 保证 HTML 解析完成后再执行；`index.css` 与 `index.js` 均相对路径，因此页面只能被**同目录静态托管**（实际由本机 HTTP 服务以 `/<pid>/index.html` 提供，见 §4）。
- `<title>` 统一为 `Armoury Peripheral`；`<html lang="en">` 固定英文，多语言靠运行时 XML 替换而非 HTML 属性。

## 4. 页面如何被加载（路由与宿主参数）

主进程（app.asar → `.webpack/main/index.js`）用 BrowserView 叠加：

~~~~js
new BrowserView({webPreferences:{nodeIntegration:true,contextIsolation:true,preload:...preload.js}})
r.webContents.loadURL("http://127.0.0.1:"+s+"/"+i+"/index.html?isElectron=1&session="+c+p)
~~~~

- `s` = HTTP 端口（常量 `ports:{http:"1042",https:"1043"}`）；`i` = PID（`6871`/`p520`/`omni`/`6862`）；`c` = session UUID；`p` = 额外参数（含 `host=127.0.0.1:1042`）。依据：`.webpack/main/index.js` 中 `targetView` 创建段与 `getDefaultServiceParams()` 返回 `{host:"127.0.0.1:"+HTTPPort}`。
- 标题栏是另一支 BrowserView，加载 `file://.../renderer/titleBar/index.html`（1280 × TITLEBAR_HEIGHT）。
- 页面侧解析这些参数（6871 index.js，`ia` 函数）：

~~~~js
new URL(r.url).searchParams  // 取 sid / logoHidden / mode / isElectron / deviceSN / dongleSN / isMAC
l="1"===i.get("isElectron")
~~~~

- 另有 `window.location.pathname.split("/")[1]` 取出路由首段（`$i`）→ 与 URL 形态 `/<pid>/index.html` 吻合。
- 结果：**页面路由 = 端口 + PID 目录**，不存在前端 router 的多级路径；页面内的「页签」只切换 React 视图（`functionList` 驱动）。

## 5. index.js / service.js 静态扫描（纯文本正则，未执行）

扫描对象（UTF-8 直接读取，统计值为字符串出现次数）：

| 文件 | 大小 | sendRequest | composeURL | new WebSocket | postMessage | chrome.webview | dongleSN | deviceSN |
|---|---|---|---|---|---|---|---|---|
| p520/index.js | 675,883 B | 50 | 49 | 6 | 52 | 2 | 52 | 115 |
| 6871/index.js | 903,964 B | 50 | 49 | 6 | 52 | 2 | 56 | 117 |
| 6862/index.js | 568,114 B | 2 | 2 | 2 | 52 | 2 | 37 | 5 |
| omni/index.js | 406,898 B | 2 | 2 | 2 | 52 | 2 | 6 | 6 |
| 6871/service.js | 621,724 B | 26 | 25 | 1 | 55 | 2 | 64 | 85 |

观察：p520/6871 是「全功能前端」（各 ~50 处 HTTP 调用），6862/omni 只有 2 处（只调设备列表/配对），与它们的极简 caps 一致。

### 5.1 HTTP 路由字符串

| 字符串 | 出现文件与次数 | 推断用途 |
|---|---|---|
| `127.0.0.1:1042` | 5 个文件各 1 次 | 默认本机服务地址（`process.argv` 无 `host=` 时的兜底）；主进程常量同值为 `ports.http` |
| `/api`（`type/1/model/6871` 拼接） | 5 个文件各 1 次 | API 基址：`http://host/api/type/<deviceType>/model/<modelNumber>`，反解 `caps.url:"type/1/model/6871"` |
| `/buttons` | p520:5、6871:5、service:2 | 读/写按键映射（`setButtons`/`setMouseButton`/`setDefaultButtons`） |
| `/performance` | p520:7、6871:7、service:2 | DPI / 轮询率 / 直线修正 / LOD |
| `/lighting` | p520:4、6871:4、service:4 | 灯效（含分区、Aura Sync 241） |
| `/calibration` | p520:1、6871:1、service:1 | 表面校准 / 拉抬高度 |
| `/power`、`/event/power` | p520:1、6871:1、service:1 | 电量、休眠时间、低电提示 |
| `/dial`、`/joystick` | p520:1、6871:1、service:1 | 摇杆/拨盘映射（本机型未启用，caps 无对应键） |
| `/profile`、`/profile/` | p520:2+3、6871:2+3、service:1+2 | 5 个 profile 的切换/保存/删除 |
| `/file`、`/file/` | p520:1+2、6871:1+2、service:1+2 | 读写 `performance1.xml` 等落盘配置（见 §10.1） |
| `/reset` | p520:2、6871:2、service:2 | 清除 FW profile（`removeFWProfiles`，DELETE） |
| `/devices`、`/systemInfo`、`/deviceInfo` | 各 1 次 | 设备列表 / 系统信息 / 设备信息 |
| `/initialize/devicePage`、`/initialize/alertPage` | 各 1 次 | 页面初始化握手（`?dongleSN=&deviceSN=&isElectron=`） |
| `/lib/<libVersion>/...` | p520:4、6871:4、6862:3、omni:1 | 库版本路由：`/lib/1/devices`、`/lib/1/.../file` |
| `/caps` | p520:1、6871:1 | 拉取设备能力（对应 `_ref/caps.json`） |
| `/language` | p520:2、6871:2 | 拉取语言 XML |
| `/WDLControl`、`/WLDSyncStatus` | p520:1、6871:1、service:1 | 与 Wireless Dongle 同步（一拖二时的灯效同步/限速提示） |
| `/demoMode`、`/update`、`/replugin`、`/collect` | 各 1–2 次 | 演示模式、固件更新、设备重插、数据采集上报 |

查询参数：`?role=`（4/4/2/2/2 次）、`?dongleSN=`、`?deviceSN=`、`?isElectron=1`、`?session=`、`sid=`、`mode=icafe`、`logoHidden=`、`isMAC=`、`host=`。其中 `session=` 在页面 JS 里 0 次（由主进程拼 URL、preload 读出后随 IPC 回传，见 §5.3）。

### 5.2 WebSocket / socket 连接字符串

| 字符串 | 出现文件与次数 | 推断用途 |
|---|---|---|
| `ws://` + host | 5 个文件各 3 次 | 通用 WS 基址（`ws:"ws://"+host`，与 `baseUrl:"http://"+host` 同源） |
| `?role=home&deviceType=<t>&pid=<pid>` | p520:2、6871:2、omni:1 | **主页通道**：接收 `DEVICE_PLUGGED`/`DEVICE_UNPLUGGED` 等热插拔事件 |
| `?role=devicePage&deviceType=…&pid=…` | p520:3、6871:3、service:19 | **设备页通道**：页面 ↔ 框架的双向命令（`{cmd,id,msg,result}`） |
| `?role=deviceService&...` | p520:4、6871:5、service:2 | **服务进程通道**：service.js 以客户端身份连回框架 |
| `?role=devicePageLogger&...` | p520:1、6871:1、service:1 | 日志通道，帧为 `{command:"writeLog",level,deviceType,log}` |
| `ws://127.0.0.1:9013` | 5 个文件各 1 次（caps 内 `alertSocketServer`） | **告警页**通道（`alertPage`） |
| `ws://127.0.0.1:9014` | 5 个文件各 1 次（`logSocketServer`） | 独立日志 socket |
| 端口 `1042` | 上述默认 host；主进程常量 `ports.http="1042"` | 框架 HTTP+WS 主端口 |
| 端口 `1043` | 主进程常量 `ports.https="1043"`（页面 JS 0 次） | HTTPS 备用端口（未在页面路径使用） |

服务端帧命令（主进程 `handleOnSocketEvent` 的 switch）：`broadcastEvent`、`writeLog`、`socketServerEvent`、`AcDiagnosisChange`；广播目标形如 `{target:{role:["devicePage","deviceService"],deviceType,modelNumber},msg:{cmd:…}}`。

### 5.3 IPC / postMessage / webview 通道

| 字符串 | 出现文件与次数 | 推断用途 |
|---|---|---|
| `chrome.webview.postMessage` | 5 个文件各 2 次 | **WebView2 兼容路径**：页面把 `{Type, PID, …}` JSON 交给宿主（与 UWP 版共用同一份代码） |
| `window.webkit.messageHandlers.acmacapp.postMessage` | 同 2 处 | **macOS 分支**（与上一行同一 try/else 链） |
| `postMessage` | 各 52–55 次 | 上述宿主桥 + iframe/Worker 通信混用 |
| `ipcRenderer` / `electronAPI` | 页面与 service.js 均为 **0** | 页面不直接碰 Electron IPC（`contextIsolation:true`，只有 preload 能碰） |
| `webview.postMessage` / `electron.postMessage` | preload.js | 暴露的两个命名空间，默认 Channel/Target 分别为 `"uwp"` / `"electron"` |
| `subscribeEvent` | preload.js | 监听 `devicePage` 事件，按 `session` 过滤后回调页面 |
| `process.argv` 中的 `host=` | service.js 1 次 | 服务进程从命令行拿 host（主进程 fork 时注入） |

preload 关键片段（2,302 B，全文可读）：

~~~~js
o.contextBridge.exposeInMainWorld("frameworkAPI",{webview:{postMessage:...},electron:{postMessage:...}})
var i=new URLSearchParams(window.location.search).get("session")   // session 由 URL 带入
o.ipcRenderer.on("devicePage", r)                                  // 只投递给同 session 的页面
~~~~

结论：页面 → 宿主 的 IPC 只有 `frameworkAPI.webview|electron` 两组 4 个方法，`session` 是隔离键（多开页面时互不串话）。

### 5.4 function key / XML 相关字符串

| 字符串 | 出现文件与次数 | 推断用途 |
|---|---|---|
| `ExecuteFunction` | **全部 0 次**（页面、service.js、app.asar 内所有 js 均无） | SDK 入口不在页面层；`ExecuteFunction(LPCWSTR xml, IntPtr* pOut)` 由更下层的宿主进程调用（未验证具体调用方） |
| `<device_type` / `<function key=` | 页面 0 次；`modules\mouse\index.js` 有 `device_type` 36 次 | XML 由主进程设备模块用 js2xml 生成，页面只发 JSON |
| `root:{device_type:{$:{key:…},device:{$:{key:…},function:{$:{key:…}}}}` | service.js 1 次（WRITE_FILE 分支） | 唯一在页面层可见的 XML 树，用于写文件：`btoa(encodeURIComponent(data))` 塞进 `file_data` |
| `data_name` | service.js 3 次、`modules\mouse\index.js` 15 次 | XML 二级节点：功能号（1=按键、2=性能、4=校准…，见 §10.4 `DATA_NUMBER`） |
| `Builder({cdata:true,xmldec:{encoding:"UTF-8",standalone:true}}).buildObject(...)` | service.js 6 次 | 用 `xmlbuilder` 把 JS 对象转 XML（CDATA 包裹） |
| `SetPerformance`/`SetEffect`/`SetButtons` | 0 次 | 真实命名是 **小驼峰**：`setPerformance`、`setLightingEffect`、`setButtons`（见下） |
| `setPerformance`/`setButtons`/`setCalibration`/`setPower`/`setLightingEffect`/`setRegionEffect`/`setDPIChannel`/`setCurrentPerformance`/`setAutoResponseEvent` | 6871 index.js 各 1–2 次（`gi.i(...)` 日志标签） | 页面动作名，与 `/performance`、`/buttons` 等 REST 资源一一对应 |

页面侧动作标签（从 6871 index.js 的日志调用提取，共 60+ 个）：`setMouseButton`、`setDefaultButtons`、`setJoystickMode`、`setCalibration`、`setDefaultCalibration`、`setPerformance`、`setDefaultPerformance`、`setDPIChannel`、`addDPIChannel`、`deleteDPIChannel`、`setPerformanceAll`、`setLightingEffect`、`setDefaultLighting`、`setLightingSwitch`、`setRegionEffect`、`setPower`、`setAutoResponseEvent`、`saveToProfile`、`syncProfile`、`syncAllProfiles`、`recoverAllProfile`、`resetAllProfiles`、`removeFWProfiles`、`deleteSoftwareProfile`、`getProfile`、`getDeviceInfo`、`getSystemInfo`、`getDemoMode`、`getWDLSyncStatus`、`GetMacroList`、`saveDialSetting`、`update FW`。

### 5.5 HID 相关字符串

| 字符串 | 出现次数 | 结论 |
|---|---|---|
| `51 31`（HEX 帧头） | 5 个文件均 0 | 页面层不组 HID 帧 |
| `WriteFile` / `Report ID` / `HID` / `AacMouseHal` | 全部 0 | 同上；`AacMouseHal_x86.dll` 在整个 app.asar 文本中也搜不到 |
| `writeLog`、`writeFile` | 有（前者是日志 WS 命令，后者是 SDK 功能号 11） | 与 HID 无关，勿混淆 |

结论（有证据）：HID 帧（`51 31 <子命令> 00` + 65 字节报告）完全位于 `AacMouseHal_x86.dll` 及以下，页面层通过 **JSON/XML 功能号** 间接驱动；页面侧看不到任何字节级协议。

### 5.6 能力开关名

以下为 caps 中的开关字段与页面/服务中的引用次数（裸串匹配，包含 `isWireless:!0` 这类字面量）：

| 开关 | p520/index.js | 6871/index.js | service.js | 含义（依据 caps） |
|---|---|---|---|---|
| `Buttons` | 1 | 3 | 3 | 功能页签 id（`functionList`），langRef `row_2` |
| `Performance` | 1 | 4 | 3 | langRef `row_3` |
| `Lighting` | 2 | 4 | 3 | langRef `row_4` |
| `Calibration` | 2 | 4 | 3 | langRef `row_5` |
| `Power` | 1 | 3 | 3 | langRef `row_207` |
| `Promotion` / `ArmouryCrateGear` | 0 / — | 4 | 1 | 商店推广页签 |
| `FirmwareUpdate` | 1 | 5 | 2 | langRef `row_77`；6862 另有 `Dongle`（1 次） |
| `isWireless` | 10 | 17 | 12 | `deviceProps.isWireless:true` → 显示电量/休眠/2.4G 相关 UI |
| `isAuraSyncEditable` | 11 | 14 | 10 | 决定 Aura Sync（effectID 241）是否可编辑 |
| `RapidFire` | 13 | 27 | 9 | 连发功能（`hasRapidFireProfile:true`，profileID `"Rapid-Fire"`） |
| `isSupportMacroShift` | 1 | 2 | 2 | 宏的 Shift 模式 |
| `isSupportCustomKeyCode` | — (caps 中为 true) | — | — | 允许自定义按键码 |

注意：p520 的 caps 没有 `functionList` 项之外的 `Promotion`，其 UI 依赖 `refModelNumber` 跳转到 6871；`Dongle` 页签只出现在 6862 的 caps（`functionList:[{id:"Dongle"},{id:"FirmwareUpdate"}]`）。

## 6. 本地服务 HTTP 路由表（主进程证据）

主进程用 express 注册（`setURL([...])`，`.webpack/main/index.js`），设备无关前缀 `/type/:deviceType/model/:modelNumber`：

| 方法 | 路径 | 说明 |
|---|---|---|
| GET | `/:resource?` | 根资源：`/devices`、`/fonts`、`/language`、`/view`、`/window`、`/install`、`/app`、`/process`（白名单常量 `T`） |
| POST | `/:resource?` | 同上，带表单（`hasFormData`） |
| GET | `/type/:deviceType/model/:modelNumber/:resource/:id?` | 设备资源读 |
| POST/PUT | `/type/:deviceType/model/:modelNumber/:resource` | 设备资源写 |
| DELETE | `/type/:deviceType/model/:modelNumber/:resource/:id` | 设备资源删（如 `/reset`、profile 删除） |
| POST | `/type/:deviceType/model/:modelNumber/file` | 文件上传（`hasFormData`） |
| GET/POST/PUT/PATCH/DELETE | `/lib/:version/type/:deviceType/model/:modelNumber/:resource/:id?` | 带库版本的同一套资源 |

- 子资源白名单：`["file","image","collect"]`；允许的扩展名 `["xml","json"]` 与图片 `["jpg","jpeg","png","bmp","gif"]`。
- 静态目录映射（`pathMapping`）：`view=<exe>/view`、`proj=<exe>`、`foreign=<exe>/dll`、`ssl=<exe>/ssl` —— 页面就是被 `<exe>/view/<pid>/` 静态托管，再以 `/<pid>/index.html` 访问。
- 因此页面里的 `/buttons`、`/performance` 等并非独立服务，而是 `/type/1/model/6871/buttons` 的相对写法（`composeURL` 拼 `caps.url`）。

## 7. resources\src\_ref：能力与按键定义

### 7.1 `_ref` 目录结构

| 页面 | `_ref` 内容 | caps.json | default.json |
|---|---|---|---|
| p520 | `caps.json` + `keyDefinitions\*.json`（22 个：11 语言 × 普通/mac） | 419 B | 无 |
| 6871 | `caps.json` + `default.json` + `keyDefinitions\*.json`（22 个） | 8,964 B | 9,783 B |
| 6862 | `caps.json` + `default.json`（无 keyDefinitions） | 1,390 B | 3 B（`{}`） |
| omni | `caps.json` + `default.json` | 207 B | 3 B（`{}`） |

### 7.2 6871 的 caps.json 全文

~~~~json
{
 "device": "ROG STRIX IMPACT III WIRELESS (2.4GHz)",
 "deviceZH": "ROG 影刃 3 无线版",
 "guid": "2e3ccb5f-07b8-481b-90e2-f4165a3ccd21",
 "productName": "P520",
 "deviceType": "1",
 "theme": "rog",
 "multiModelNumber": [
  "6871",
  "6873"
 ],
 "profileCount": 5,
 "profiles": {
  "hasRapidFireProfile": true
 },
 "deviceProps": {
  "isWireless": true,
  "isAuraSyncEditable": true,
  "isSupportMacroShift": true,
  "isSupportCustomKeyCode": true
 },
 "functionList": [
  {
   "id": "Buttons",
   "text": "Buttons",
   "langRef": "row_2"
  },
  {
   "id": "Performance",
   "text": "Performance",
   "langRef": "row_3"
  },
  {
   "id": "Lighting",
   "text": "Lighting",
   "langRef": "row_4"
  },
  {
   "id": "Calibration",
   "text": "Calibration",
   "langRef": "row_5"
  },
  {
   "id": "Power",
   "text": "Power",
   "langRef": "row_207"
  },
  {
   "id": "Promotion",
   "text": "ArmouryCrateGear",
   "langRef": "Armoury Crate Gear"
  },
  {
   "id": "FirmwareUpdate",
   "text": "Firmware Update",
   "langRef": "row_77"
  }
 ],
 "buttons": {
  "buttonList": {
   "left": {
    "text": "row_260",
    "items": [
     {
      "id": "6",
      "name": "row_13",
      "defaultKey": "LeftForward",
      "position": "left",
      "isSupportRapidFire": true
     },
     {
      "id": "7",
      "name": "row_12",
      "defaultKey": "LeftBackward",
      "position": "right",
      "isSupportRapidFire": true
     }
    ]
   },
   "top": {
    "text": "row_259",
    "items": [
     {
      "id": "1",
      "name": "row_46",
      "defaultKey": "LeftButton",
      "position": "left"
     },
     {
      "id": "4",
      "name": "row_8",
      "defaultKey": "ScrollUp",
      "position": "left"
     },
     {
      "id": "3",
      "name": "row_11",
      "defaultKey": "ScrollClick",
      "position": "left"
     },
     {
      "id": "5",
      "name": "row_9",
      "defaultKey": "ScrollDown",
      "position": "right"
     },
     {
      "id": "2",
      "name": "row_47",
      "defaultKey": "RightButton",
      "position": "right"
     }
    ]
   }
  },
  "keyAssignFunctionList": [
   {
    "id": "0",
    "iconClassName": "optionIcon1",
    "text": "row_6",
    "subFunction": {
     "type": "dropdownList",
     "label": "row_6",
     "default": "LeftButton",
     "items": [
      {
       "id": "LeftButton",
       "name": "row_46"
      },
      {
       "id": "RightButton",
       "name": "row_47"
      },
      {
       "id": "ScrollClick",
       "name": "row_11"
      },
      {
       "id": "ScrollUp",
       "name": "row_8"
      },
      {
       "id": "ScrollDown",
       "name": "row_9"
      },
      {
       "id": "DPI1",
       "name": "row_10"
      },
      {
       "id": "LeftBackward",
       "name": "row_12"
      },
      {
       "id": "LeftForward",
       "name": "row_13"
      },
      {
       "id": "DPI2",
       "name": "row_230"
      },
      {
       "id": "DoubleClick",
       "name": "row_14"
      }
     ]
    }
   },
   {
    "id": "10",
    "iconClassName": "optionIcon14",
    "text": "row_479",
    "subFunction": {
     "type": "rapidFire",
     "default": "279",
     "items": [
      {
       "id": "279",
       "name": "row_480"
      },
      {
       "id": "23",
       "name": "row_481"
      }
     ],
     "intervalTime": {
      "start": 2,
      "end": 1000,
      "step": 2
     }
    }
   },
   {
    "id": "1",
    "iconClassName": "optionIcon2",
    "text": "row_7",
    "subFunction": {
     "type": "comboKeyInputBox",
     "label": "row_7",
     "content": {
      "text": "row_179"
     },
     "default": "",
     "placeHolder": "row_441"
    }
   },
   {
    "id": "4",
    "iconClassName": "optionIcon3",
    "text": "row_15",
    "hide": false,
    "subFunction": {
     "type": "dropdownList",
     "label": "row_86",
     "default": "-1",
     "items": [
      {
       "id": "-1",
       "name": "row_87",
       "disabled": true
      }
     ]
    }
   },
   {
    "id": "3",
    "iconClassName": "optionIcon5",
    "text": "row_24",
    "subFunction": {
     "type": "dropdownList",
     "label": "row_24",
     "default": "VolumeUp",
     "items": [
      {
       "id": "VolumeUp",
       "name": "row_25"
      },
      {
       "id": "VolumeDown",
       "name": "row_26"
      },
      {
       "id": "Mute",
       "name": "row_27"
      },
      {
       "id": "PlayPause",
       "name": "row_28"
      },
      {
       "id": "PreviousTrack",
       "name": "row_29"
      },
      {
       "id": "NextTrack",
       "name": "row_30"
      },
      {
       "id": "Stop",
       "name": "row_31"
      },
      {
       "id": "MicMute",
       "name": "row_1873"
      }
     ]
    }
   },
   {
    "id": "2",
    "iconClassName": "optionIcon4",
    "text": "row_17",
    "subFunction": {
     "type": "dropdownList",
     "label": "row_17",
     "default": "0",
     "items": [
      {
       "id": "0",
       "name": "row_18"
      },
      {
       "id": "1",
       "name": "row_19"
      },
      {
       "id": "2",
       "name": "row_20"
      },
      {
       "id": "3",
       "name": "row_21"
      },
      {
       "id": "4",
       "name": "row_22"
      },
      {
       "id": "5",
       "name": "row_23"
      }
     ]
    }
   },
   {
    "id": "11",
    "iconClassName": "optionIcon15",
    "text": "row_634",
    "subFunction": {
     "type": "message",
     "content": "row_635"
    }
   },
   {
    "id": "7",
    "iconClassName": "optionIcon6",
    "text": "row_85",
    "subFunction": {
     "type": "inputBox",
     "label": "row_91",
     "content": {
      "langRef": "Character limit: 250"
     },
     "default": "",
     "placeHolder": "row_92"
    }
   },
   {
    "id": "8",
    "iconClassName": "optionIcon12",
    "text": "row_396",
    "subFunction": {
     "type": "fileBrowser",
     "label": "row_397",
     "default": ""
    }
   },
   {
    "id": "9",
    "iconClassName": "optionIcon13",
    "text": "row_75",
    "subFunction": {
     "type": "message",
     "content": "row_398"
    }
   },
   {
    "id": "-1",
    "iconClassName": "optionIcon8",
    "text": "row_16",
    "subFunction": {
     "type": "message",
     "content": "row_178"
    }
   }
  ],
  "supportless": {
   "shiftModeMacro": [
    "ScrollUp",
    "ScrollDown"
   ],
   "targetFocus": [
    "ScrollUp",
    "ScrollDown"
   ]
  }
 },
 "lighting": {
  "hasRegionEffect": false,
  "hasAuraSync": true,
  "highestBrightnessValue": "4",
  "effectList": [
   {
    "id": "0",
    "text": "row_210",
    "className": "statics",
    "items": [
     {
      "id": "colorLevel",
      "text": "row_45",
      "options": [
       {
        "id": "Single"
       }
      ]
     },
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   },
   {
    "id": "1",
    "text": "row_211",
    "className": "breathing",
    "items": [
     {
      "id": "colorLevel",
      "text": "row_45",
      "options": [
       {
        "id": "Single"
       }
      ]
     },
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   },
   {
    "id": "2",
    "text": "row_212",
    "className": "colorcycle",
    "items": [
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   },
   {
    "id": "4",
    "text": "row_217",
    "className": "reactive",
    "items": [
     {
      "id": "colorLevel",
      "text": "row_45",
      "options": [
       {
        "id": "Single"
       }
      ]
     },
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   },
   {
    "id": "6",
    "text": "row_223",
    "className": "batterymode",
    "isPartitionDisabled": true,
    "items": [
     {
      "id": "battery",
      "text": "row_45",
      "desc": "row_188",
      "low": "20",
      "charging": "80"
     },
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   },
   {
    "id": "241",
    "text": "Aura Sync",
    "className": "auraSync",
    "excludeOrder": true,
    "isPartitionDisabled": true,
    "items": [
     {
      "id": "brightness",
      "text": "row_66",
      "options": [
       {
        "id": "0",
        "text": "0%"
       },
       {
        "id": "1",
        "text": "25%"
       },
       {
        "id": "2",
        "text": "50%"
       },
       {
        "id": "3",
        "text": "75%"
       },
       {
        "id": "4",
        "text": "100%"
       }
      ]
     }
    ]
   }
  ]
 },
 "performance": {
  "hasSupportBooster": true,
  "hasDPILighting": true,
  "isDPIColorEditable": true,
  "isSupportOSD": true,
  "hasDynamicDPIChannel": true,
  "hasDPIIndicator": true,
  "dpiAmount": "4",
  "hasDPIXYSensitivity": false,
  "indexShift": 1,
  "items": [
   {
    "id": "dpi1",
    "text": "row_43",
    "start": 100,
    "end": 36000,
    "step": 50,
    "default": 400,
    "dpiNumber": "1"
   },
   {
    "id": "dpi2",
    "text": "row_43",
    "start": 100,
    "end": 36000,
    "step": 50,
    "default": 800,
    "dpiNumber": "2"
   },
   {
    "id": "dpi3",
    "text": "row_43",
    "start": 100,
    "end": 36000,
    "step": 50,
    "default": 1600,
    "dpiNumber": "3"
   },
   {
    "id": "dpi4",
    "text": "row_43",
    "start": 100,
    "end": 36000,
    "step": 50,
    "default": 3200,
    "dpiNumber": "4"
   },
   {
    "id": "pollingRate_Booster",
    "text": "row_33",
    "default": "6",
    "options": [
     {
      "id": "0",
      "text": "125"
     },
     {
      "id": "1",
      "text": "250"
     },
     {
      "id": "2",
      "text": "500"
     },
     {
      "id": "3",
      "text": "1000"
     },
     {
      "id": "4",
      "text": "2000"
     },
     {
      "id": "5",
      "text": "4000"
     },
     {
      "id": "6",
      "text": "8000"
     }
    ],
    "notice": "row_1775",
    "replacement": "ROG Omni"
   },
   {
    "id": "pollingRate_Common",
    "text": "row_33",
    "default": "3",
    "options": [
     {
      "id": "0",
      "text": "125"
     },
     {
      "id": "1",
      "text": "250"
     },
     {
      "id": "2",
      "text": "500"
     },
     {
      "id": "3",
      "text": "1000"
     }
    ]
   },
   {
    "id": "angleSnapping",
    "text": "row_32",
    "default": "0",
    "options": [
     {
      "id": "0",
      "text": "off"
     },
     {
      "id": "1",
      "text": "on"
     }
    ]
   },
   {
    "id": "angleTuning",
    "text": "row_52",
    "default": "3",
    "options": [
     {
      "id": "0",
      "text": "-20"
     },
     {
      "id": "1",
      "text": "-10"
     },
     {
      "id": "2",
      "text": "-5"
     },
     {
      "id": "3",
      "text": "0"
     },
     {
      "id": "4",
      "text": "5"
     },
     {
      "id": "5",
      "text": "10"
     },
     {
      "id": "6",
      "text": "20"
     }
    ]
   }
  ]
 },
 "calibration": {
  "hasManualCalibration": true,
  "hasPresetSurfaces": true,
  "noCalibrationCanStillSetLOD": true,
  "mousePadList": [
   {
    "id": "255",
    "tooltip": "Default (No Calibration)"
   },
   {
    "id": "20",
    "tooltip": "ROG Moonstone Ace L"
   },
   {
    "id": "21",
    "tooltip": "ROG Moonstone Ace L"
   },
   {
    "id": "22",
    "tooltip": "ROG Hone Ace XXL"
   },
   {
    "id": "18",
    "tooltip": "ROG Hone Ace Aim Lab Edition"
   },
   {
    "id": "19",
    "tooltip": "ROG Scabbard II EVA EDITION"
   },
   {
    "id": "12",
    "tooltip": "ROG Scabbard II"
   },
   {
    "id": "13",
    "tooltip": "ROG Sheath Gundam LTD"
   },
   {
    "id": "11",
    "tooltip": "Balteus QI",
    "showConfirm": true
   },
   {
    "id": "10",
    "tooltip": "Balteus",
    "showConfirm": true
   },
   {
    "id": "7",
    "tooltip": "ROG Scabbard"
   },
   {
    "id": "8",
    "tooltip": "ROG Strix Edge"
   },
   {
    "id": "14",
    "tooltip": "ROG Strix Edge Call of Duty - Black Ops 4 Edition"
   },
   {
    "id": "0",
    "tooltip": "ROG Whetstone"
   },
   {
    "id": "1",
    "tooltip": "ROG Sheath"
   },
   {
    "id": "15",
    "tooltip": "ROG Sheath BLK LTD"
   },
   {
    "id": "16",
    "tooltip": "ROG Sheath PNK LTD"
   },
   {
    "id": "17",
    "tooltip": "ROG Sheath Electro Punk"
   }
  ],
  "liftOffDistanceList": [
   {
    "id": "0",
    "text": "row_48"
   },
   {
    "id": "1",
    "text": "row_50"
   }
  ]
 },
 "firmwareUpdate": {
  "versions": [
   {
    "name": "row_323",
    "dataKey": "fwVersion"
   },
   {
    "name": "row_325",
    "dataKey": "usbVersion"
   },
   {
    "name": "row_326",
    "dataKey": "rfVersion"
   },
   {
    "name": "row_2147",
    "dataKey": "boosterVersion"
   }
  ]
 },
 "power": {
  "hasPowerAlert": true,
  "hasSleepTime": true,
  "isFiveStages": true,
  "isFuzzyPower": true,
  "blinkingLights": [
   {
    "id": "0",
    "text": "row_239"
   },
   {
    "id": "1",
    "text": "10%"
   },
   {
    "id": "2",
    "text": "20%"
   },
   {
    "id": "3",
    "text": "30%"
   },
   {
    "id": "4",
    "text": "40%"
   },
   {
    "id": "5",
    "text": "50%"
   }
  ]
 },
 "modelNumberMapping": {
  "dongle": "6871",
  "bluetooth": "6873"
 },
 "profileHotKey": {
  "MainKey": [
   "201",
   "202",
   "203",
   "204",
   "205"
  ],
  "instructionType": "18",
  "showNoticeInButtonPage": true,
  "specificKeys": true
 }
}
~~~~

### 7.3 按键定义体系（keyDefinitions）

- 目录：`View\6871\resources\node_modules\ac-components\src\_ref\keyDefinitions\`，22 个文件，2.0–2.2 KB/个：`de-DE, de-DE_mac, en-US, en-US_mac, es-ES, es-ES_mac, fr-FR, fr-FR_mac, hu-HU, hu-HU_mac, it-IT, it-IT_mac, ja-JP, ja-JP_mac, no-NO, no-NO_mac, pt-PT, pt-PT_mac, tr-TR, tr-TR_mac, uk-UK, uk-UK_mac`。
- 结构是**二维字符串数组**（不是 key→value），共 28 行 × 8 列，行序与布局对应物理键盘的「列优先」扫描表。en-US 前 3 行：

~~~~json
[["Esc","`","Tab","Caps Lock","L-Shift","L-Ctrl","1","2"],
 ["F1","3","E","D","X","C","Q","W"],
 ["F2","4","R","F","V","Code131","A","S"], …]
~~~~

- 第 17–23 行是**鼠标/摇杆专用键码区**，正是 7 键映射的来源：

~~~~json
["LeftButton","RightButton","ScrollClick","ScrollUp","ScrollDown","DPI1","DPI2","DISABLE"],
["LeftBackward","LeftForward","RightBackward","RightForward","Button 7","Button 8","Button 9","Button 10"],
["Button 11","Button 12","DoubleClick","","Joystick FWD Axis 0","Joystick REV Axis 0","Joystick FWD Axis 1","Joystick REV Axis 1"],
["PlayPause","Stop","PreviousTrack","NextTrack","Mute","VolumeDown","VolumeUp",""],
["Profile0","Profile1","Profile2","Profile3","Profile4","Profile5","FastForward","FastBackward"],
["Joystick Up Arrow", … "Joystick Button 4"],
["Slide Forward", … "Zoom Out"],
["Rapid-Fire Toggle","Rapid-Fire Shift","SideKey1"…"SideKey6"],
["ASUS Dial","MS Dial","BT Swich"],[],[],[],["ScrollLeft","ScrollRight"]
~~~~

- `_mac` 版本与普通版**逐项相同**，只把 `L-Win/L-Alt/R-Alt` 换成 `L-⌘/L-⌥/R-⌥`、空位补 `R-⌘`（en-US_mac 第 13 行末列）。
- P520 的 7 个物理键在这套体系里的落点是 `caps.buttons.buttonList`：`top`（正面：id 1 左键、2 右键、3 滚轮点击、4 上滚、5 下滚）+ `left`（左侧：id 6 前进 `LeftForward`、id 7 后退 `LeftBackward`），**共 7 个可编程键**，与 `keyAssignFunctionList` 的 12 种功能（鼠标功能/键盘功能/宏/多媒体/Windows 快捷方式/连发/预设文本/启动程序/隐身/禁用…）组合。
- 注意 service.js 内嵌的是同一份表，但只内嵌 `en-US`（`keyDefinitions["en-US"]`），其余 21 份运行时从 `_ref` 或 `/language` 接口取。

### 7.4 default.json（出厂默认值，仅 6871 有）

9,783 B，键与 caps 功能段同名：

| 段 | 内容要点 |
|---|---|
| `buttons` | 仅 `profileID:"Rapid-Fire"` 一个 profile；左键/右键/滚轮点击 `disable:true`（不可改），7 号键默认 `buttonFunction:"10"`（RAPID_FIRE）、`key_1:"23"`、`key2Value:"50"` |
| `lighting` | 5 个 profile（1–5）各 6 条 effect：`0 Static / 1 Breathing / 2 Colorcycle(默认) / 4 Reactive / 6 Battery / 241 Aura Sync`；颜色分别为 红/洋红/蓝/绿/黄，亮度 2（50%）、速度 1 |
| `performance` | `dpi1..4 = 7/15/31/63`（即 400/800/1600/3200）、`pollingRate_Booster:"6"`(8000)、`pollingRate_Common:"3"`(1000)、`angleSnapping:"0"`、`angleTuning:"3"`(0°)、`currentDPI:"3"`，DPI 指示灯颜色 `255,0,0 / 193,0,255 / 0,61,255 / 49,255,0` |
| `calibration` | `calibrationFunction:"255"`(NONE) + `calibrationHeight:"0"` |
| `power` | `timeToSleep:"2"`(3 分钟)、`lowPowerToShine:"2"`(20%) |

## 8. 语言 XML 体系

- 生成物：`resources\node_modules\ac-components\src\_ref\language\` 下 **24 个** `<Language>.xml`（Czech, Danish, Dutch, English, Finnish, French, German, Indonesia, Italian, Japanese, Korean, Norwegian, Polish, Portuguese, Romanian, Russian, Simplified Chinese, Slovakian, Spanish, Swedish, Thai, Traditional Chinese, Turkish, Ukrainian）。
- p520 的 `resources` 根目录另有一份**带 hash 的拷贝**（webpack 资源命名）：`English-68efc.xml`（149,820 B）、`Simplified Chinese-*.xml` 等 24 个；6871 两者都有（48 个 xml）。
- 格式：单层扁平 key→文本，key 即 `row_<n>`，n 与代码里的 `langRef` 直接对应：

~~~~xml
<root>
  <row_2>Buttons</row_2>
  <row_3>Performance</row_3>
  <row_43>DPI</row_43>
</root>
~~~~

| 文件 | 条目数 | 最大 key | 备注 |
|---|---|---|---|
| 6871 English.xml | 2,294 | row_2297 | 全量文案（含键盘/耳机等其它品类残留） |
| 6871 Simplified Chinese.xml | 2,294 | row_2297 | 与英文**条目完全对齐**（2294/2294） |
| 6862 English.xml | 112 | — | 只保留接收器页用到的条目 |
| omni English.xml | 14 | — | 极限裁剪：Reset/Cancel/OK/Page Error/Retry/Go to landing page… |
| p520 English-68efc.xml | 2,294 | row_2297 | 与 6871 同一份（字节数相同 149,820） |

- 关键条目抽样（English / 简体中文）：

| key | English | 简体中文 | 用在哪 |
|---|---|---|---|
| row_2 | Buttons | 按键 | 页签 |
| row_3 | Performance | 性能 | 页签 |
| row_4 | Lighting | 灯光 | 页签 |
| row_5 | Calibration | 校准 | 页签 |
| row_207 | Power | 电量 | 页签 |
| row_77 | Firmware Update | 固件更新 | 页签 |
| row_43 | DPI | DPI | 性能段 |
| row_44 | Lift-off Distance | 拉抬高度 | 校准段 |
| row_32 / row_33 | Angle Snapping / Polling rate | 直线修正 / 轮询率 | 性能段 |
| row_66 | LED Brightness | LED 亮度 | 灯光段 |
| row_259 / row_260 | Top / Left | 正面 / 左侧 | 按键分区标题 |
| row_479 / row_480 | Rapid-Fire activator / Shift mode - hold to activate | 连发启动键 / 按压模式… | 连发 |
| row_323 / row_325 / row_326 / row_2147 | Device version: / Dongle (USB) version: / Dongle (2.4GHz) version: / Booster version: | 设备当前版本：/ 接收器 (USB) 当前版本：/ 接收器 (2.4GHz)当前版本：/ 加速器版本： | 固件更新四行 |
| row_1775 | When two devices are paired to the same {0} … polling rate is limited to 1000 Hz. | …轮询频率的上限为 1000 Hz。 | `performance.pollingRate_Booster.notice` |
| row_1865 / row_1866 | RF 2.4GHz / Bluetooth | 无线接收器 / 蓝牙 | p520 `connectList` |
| row_545 / row_1597 / row_1601 | Mouse / Receiver / Keyboard | 鼠标 / 接收器 / 键盘 | 6862 配对白名单 |
| row_239 | Never | 永不 | 休眠时间第 6 档 |

- 文案分组（按 key 区间推断）：2–130 通用控件与鼠标功能名；130–260 校准/灯光分区/按键区标题；260–500 各功能页标签与帮助文案；500–1000 键盘/耳机/摇杆遗留；1570–1620 设备类型名；1700–1800 提示与限速说明；2100–2297 固件版本行与新版通知。
- 推断：语言包是**跨品类共用的大字典**（2,294 条对一款鼠标明显过剩），页面构建时再按需裁剪（omni/6862 的小文件即为裁剪结果）。

## 9. app.asar 结构（自解析，未用 asar 包）

解析方式：读前 16 字节 → `[4, 207500, 207496, 207490]`，即 44 字节 pickle 头（`uint32 4` + 头长度 207,500 + 载荷长度 207,496 + JSON 串长 207,490），JSON 自偏移 16 开始，**数据区起点 = 207,508**。

| 顶层条目 | 类型/大小 | 说明 |
|---|---|---|
| `.webpack` | 目录（19 个文件） | 编译产物 |
| `node_modules` | 目录（133 个子项，**126 个含 package.json**） | 运行时依赖（express/ws/multer/sharp/log4js…） |
| `.sign` | 目录（6 个文件） | 待签名/已签名二进制清单 |
| `package.json` | 3,417 B | 见下 |
| `forge.config.ts` | 10,895 B | electron-forge 构建配置（打包脚本残留） |
| `babel.config.js` | 507 B | 同上 |
| `icons` | 目录 | 应用图标 |

### 9.1 `.webpack` 清单

| 路径 | 大小 | 角色 |
|---|---|---|
| `main/index.js` | 1,421,460 B | **主进程**：express 服务、BrowserView 管理、设备服务进程调度、WS 广播 |
| `main/index.js.map` | 3,986,174 B | sourcemap（未压缩源码可还原） |
| `main/sdk.js` | 254,574 B | SDK 侧封装（含 `device_type` XML 逻辑；**不含** `ExecuteFunction` 字符串） |
| `main/sdk.js.map` | 904,883 B | sourcemap |
| `renderer/window/index.html` | 579 B | 主窗口壳（`<title>Armoury Crate Gear</title>`，仅一个 `.block` 容器） |
| `renderer/window/index.js` | 134 B | 壳脚本（几乎为空，实际内容全由远端页面填充） |
| `renderer/window/preload.js` | 2,302 B | **暴露 `frameworkAPI`**（见 §5.3） |
| `renderer/titleBar/index.html` | 184 B | 标题栏壳（`<div id="root">`） |
| `renderer/titleBar/index.js` | 153,072 B | 标题栏 React 应用 |
| `renderer/*.svg / icon.png` | 631–3,767 B | 返回/更多按钮图标 |

### 9.2 `.sign` 目录含义

6 个条目：`addon/ac_node_addon.node`（463,704 B）、`electron/{ffmpeg.dll, libEGL.dll, libGLESv2.dll, vk_swiftshader.dll, vulkan-1.dll}`。

- 主进程 `index.js` 中 `.sign` 出现 99 次 → 推断为**启动前的完整性校验/重签名清单**（Electron 自带的这几个 DLL 在安装目录里会被替换为华硕签名版；`.sign` 存放待覆盖副本）。
- 与 `forge.config.ts` 的 `signArtifact`、`@electron-forge/plugin-fuses`、`@electron/fuses` 依赖呼应（构建期签名产物）。
- 具体校验算法未验证（需要读 `.webpack/main/index.js` 的 hash/digest 逻辑，属另一篇范围）。

### 9.3 package.json 关键字段

~~~~json
{
 "name": "asus_framework",
 "version": "4.2.2.8",
 "main": ".webpack/main",
 "backup": {
  "newmain": ".webpack/main",
  "main": "build/src/index.js",
  "bin": {
   "asus_framework": "build/src/index.js"
  }
 },
 "author": "ASUSTeK COMPUTER INC.",
 "license": "MIT",
 "scripts": {
  "tsc": "tsc && tsc -w",
  "build": "node ./release/build.js",
  "dev": "node ./release/build.js --dev",
  "start": "electron-forge start --inspect-electron -- --mode=development",
  "package": "electron-forge package",
  "make": "electron-forge make",
  "rebuild": "electron-rebuild -f",
  "signArtifact": "node ./release/signArtifact.js"
 },
 "assets": [
  "node_modules/ac_node_addon/**/*",
  "node_modules/sharp/**/*"
 ],
 "dependencies": {
  "ac_node_addon": "git+ssh://julio_yu@tp-biosrd-v02.corpnet.asus:29418/AC_Node_Addon",
  "cors": "2.8.5",
  "express": "4.21.0",
  "helmet": "7.0.0",
  "log4js": "6.9.1",
  "multer": "v1.4.5-lts.1",
  "pidusage": "^3.0.2",
  "selfsigned": "2.1.1",
  "sharp": "0.33.2",
  "uuid": "8.3.2",
  "ws": "8.17.1",
  "xml2js": "0.5.0"
 }
}
~~~~

- `main: ".webpack/main"`、`backup.main: "build/src/index.js"` —— 说明生产包只带 webpack 产物，源码入口仅备份。
- 运行时依赖里 `ac_node_addon` 来自华硕内网 git（`git+ssh://julio_yu@tp-biosrd-v02.corpnet.asus:29418/AC_Node_Addon`），即 `ExternalFiles` 下的原生插件；`sharp`/`express`/`ws`/`multer` 组成「本地 HTTP + 图片处理」能力。
- `electron 29.4.6`、`react 18.2.0` 与二进制目录版本（`version` 文件、asus_framework.exe）一致。

## 10. service.js 专项（页面侧服务层，621,724 B）

### 10.1 启动方式与参数

主进程（`.webpack/main/index.js`，类 `DeviceService`）把 service.js 拉成**独立子进程**：

~~~~js
l = path.resolve(pathMapping.proj + "/view/" + modelNumber + "/service.js")
if (fs.existsSync(l)) {
  params = Object.keys(o).map(k => k + "=" + o[k])            // host=127.0.0.1:1042 …
  cpu.utilityProcess.fork(l, params, {serviceName:"Device Service process - " + modelNumber})
}
~~~~

service.js 末尾自解析命令行：

~~~~js
rc = process.argv.reduce((e,n)=>{var [k,v]=n.split("="); e[k]=v; return e;},{})
nc(rc)                     // nc = e => new tc(e)  —— tc 即设备服务主类
module.exports = {service:nc, configSettings:oc, constantMouse:D}
~~~~

- 服务内把 `globalThis.globalVariable.currentApp` 固定为 `"Device Service"`，日志前缀 `"[Device Service]"`。
- host 兜底链：`globalThis.location.host` → `process.argv` 里的 `host=` → `"127.0.0.1:1042"`。
- 由 `baseUrl = "http://" + host`、`ws = "ws://" + host` 派生全部端点，再拼 `caps.url`（`type/1/model/6871`）。
- 进程生命周期由主进程管：`kill()` → `childProcess.kill()`；`handleOnProcClosed` 广播 `SDK_PROCESS_CRASHED` 给 `devicePage/alertPage/deviceService`。

### 10.2 数据落地（服务层持久化）

`C:\ProgramData\ASUS\ArmourySDK\MouseSDK\P520\` 实测内容：

| 文件 | 大小 | 内容 |
|---|---|---|
| `202410050572\button1.xml` | 12,542 B（UTF-16LE） | 按键映射：`<device_type key="1"><device key="6871"><data_name key="1"><button_function_1>0…<button_1_key_1>16…` |
| `performance1.xml` | 725 B | `<data_name key="2">`：dpi_1..4 = 7/15/31/75、polling_rate=3、angle_tuning=3、button_response=-1、high_performance_mode=-1 |
| `calibration1.xml` | 478 B | `<data_name key="4">`：calibration_function=1、material=-1、height=0、result=-1 |
| `effect1.xml` | 5,798 B | 灯效 profile（同 §7.4 lighting 结构） |

- 目录名 `202410050572` = deviceSN（服务里 `getState().settings.deviceSN`）。文件名 `<section><profileIndex>.xml`，profileIndex 对应 caps `profileCount:5`。
- XML 根结构与 §10.4 的 `DATA_NUMBER` 完全对应：`data_name key` 即功能号（1 按键 / 2 性能 / 4 校准 / 5 电量 / 3 灯效）→ **这就是送到 `ExecuteFunction` 的那份 XML 的持久化形态**（推断，仅形态一致，未抓到运行时字节）。

### 10.3 内嵌 caps（与 `_ref/caps.json` 同源）

service.js 开头 `var c={settings:{caps:{…}}}` 段，逐键与 caps.json 对应（8 KB 级），JS 字面量省掉了 JSON 的引号。段清单：

| 段 | 关键内容 |
|---|---|
| `device/deviceZH/productName/guid/deviceType/theme` | `ROG STRIX IMPACT III WIRELESS (2.4GHz)` / `ROG 影刃 3 无线版` / `P520` / `2e3ccb5f-…` / 1 / rog |
| `multiModelNumber` | `["6871","6873"]` —— 同一产品的 2.4G 与蓝牙两个 PID |
| `profiles` | `{hasRapidFireProfile:true}` |
| `deviceProps` | `isWireless / isAuraSyncEditable / isSupportMacroShift / isSupportCustomKeyCode` 全 true |
| `functionList` | 6 个页签 + 推广，各带 `langRef` |
| `buttons` | `buttonList.top/left` + `keyAssignFunctionList` 12 项 + `supportless`（宏 shift 不支持滚轮） |
| `lighting` | `hasRegionEffect:false`、`hasAuraSync:true`、`highestBrightnessValue:"4"`、6 种效果 |
| `performance` | `hasSupportBooster:true`、`hasDPILighting:true`、`dpiAmount:"4"`、8 项参数 |
| `calibration` | 18 种预设鼠标垫 + LOD 2 档 |
| `firmwareUpdate` | 4 个版本行：fwVersion / usbVersion / rfVersion / boosterVersion |
| `power` | `hasPowerAlert/hasSleepTime/isFiveStages/isFuzzyPower` + 6 档闪烁灯（0/10/20/30/40/50%） |
| `modelNumberMapping` | `{dongle:"6871", bluetooth:"6873"}` |
| `profileHotKey` | `MainKey:["201".."205"]`、`instructionType:"18"` |
| `defaultSettings` | 与 `_ref/default.json` 同构（按键/灯效/性能/校准/电量出厂值） |

### 10.4 协议常量（页面↔服务的枚举，service.js 内 `Object.freeze`）

`FUNCTION_NUMBER`（`function.key`，即 XML 里的 `<function key>`）：

| 名 | 值 | 名 | 值 |
|---|---|---|---|
| KEY_CHANGE | 0 | FRAME | 8 |
| PERFORMANCE | 1 | KEY_LOG | 9 |
| EFFECT | 2 | QUERY | 10 |
| CALIBRATION | 3 | WRITE_FILE | 11 |
| POWER | 4 | DELETE_FILE | 12 |
| PROFILE | 5 | AUTO_RESPONSE | 13 |
| LOAD_DATA | 6 | DIAL_SETTING | 15 |
| SYNC | 7 | | |

`DATA_NUMBER`（`data_name.key`，查询子类）：

| 名 | 值 | 名 | 值 |
|---|---|---|---|
| DEVICE_INFO | 0 | GET_KEY_STATE | 6 |
| BUTTON_INFO | 1 | GET_KEY_COUNT | 7 |
| PERFORMANCE_INFO | 2 | GET_FRAME | 8 |
| EFFECT_INFO | 3 | READ_FILE | 9 |
| CALIBRATION_INFO | 4 | DONGLE_DEVICE_INFO | 10 |
| POWER_INFO | 5 | SYSTEM_INFO | 14 |

`SOCKET_EVENT`（事件号，三位字符串）：`MOUSE_KEYEVENT 001`、`MOUSE_ALIVE 002`、`AUTO_RESPONSE_EVENT 003`、`DPI_CHANNEL_EVENT 004`、`MOUSE_RAPID_FIRE_EVENT 005`、`MONITOR_CHANGE_EVENT 006`、`DPI_TUNING_EVENT 007`、`POLLING_RATE_EVENT 008`、`LOD_EVENT 009`、`HW_RESET_EVENT 010`、`SDK_SWITCH_PROFILE 011`、`WDL_SYNC_EVENT 012`、`FW_UPDATE_EVENT 013`。

其它枚举：

| 枚举 | 内容 |
|---|---|
| AREA（灯区） | LOGO 0 / SCROLL_WHEEL 1 / BASE 2 / GLOBAL 3 / SIDE 4 / DPI 5 / FRONT 6 / DOCK 7 |
| EFFECT_ID | STATIC 0 / BREATHING 1 / COLOR_CYCLE 2 / WAVE 3 / REACTIVE 4 / COMENT 5 / BATTERY 6 / OFF 240 / AURA 241 |
| BRIGHTNESS_PERCENTAGE | 0/1/2/3/4 = 0/25/50/75/100% |
| CALIBRATION_FUNCTION | DEFAULT 0 / MANUAL 1 / NONE 255 |
| TIME_TO_SLEEP_MIN | 1/2/3/5/10/Never = 0..5 |
| LOW_POWER_PERCENTAGE | 10/15/20/25/30 = 0..4 |
| PROFILE_FUNCTION | RECOVER_ALL 0 / RECOVER_CURRENT_SETTING 1 / RECOVER_CURRENT_EFFECT 2 / SAVE_TO_PROFILE 3 / CHANGE_TO_PROFILE 4 |
| DEVICE_MODE | NORMAL 0 / BOOT_LOADER 1 / WIRELESS 2 / AURA_SYNC 3 / ICAFE 4 |
| MAIN_FUNCTION（按键功能大类） | UNSET -1 / MOUSE 0 / KEYBOARD 1 / WINDOW_SHORTCUT 2 / MULTIMEDIA 3 / MACRO 4 / GAMEPAD 5 / JOYSTICK 6 / CLIPBOARD 7 / SCREENSHOT 8 / STEALTH 9 / RAPID_FIRE 10 / LAUNCH_AC 11 / DIALMODE 12 / BT_HOST_SWITCH 13 / FIND_CURSOR 14 / GESTURE 15 / COPILOT 16 |
| MOUSE_BUTTON（键号） | LEFT 1 / RIGHT 2 / MIDDLE 3 / SCROLL_UP 4 / SCROLL_DOWN 5 / DPI_1 6 / DPI_2 7 / BACK_LEFT 8 / FORWARD_LEFT 9 / BACK_RIGHT 10 / FORWARD_RIGHT 11 / BUTTON_7..12 = 12..17 / JOYSTICK_UP..RIGHT = 18..21 |
| 语言码映射 | `{3:"de-DE",4:"uk-UK",5:"fr-FR",7:"hu-HU",8:"it-IT",11:"no-NO",12:"pt-PT",15:"es-ES",17:"en-US",18:"tr-TR",21:"ja-JP"}`（UWP 语言 id → BCP-47） |
| 系统事件号 | WEBSOCKET_TARGET_NOT_FOUND 20000 / POWER_INFO 20101 / POWER_TRAY_ICON 20102 |

### 10.5 service.js 暴露的 API 面

模块导出：`{service: (argv)=>DeviceService, configSettings: caps.settings, constantMouse: D}`。实例方法（webpack `key:"…"` 提取，共 100+，按层归类）：

| 层 | 方法 |
|---|---|
| 传输 | `connect`、`connectSocketServer`、`connectFramwork`、`sendSocketServer`、`broadcastEvent`、`send`、`composeURL`、`sendRequest`、`xmlParser`、`toObject` |
| 事件 | `addEventListener`、`removeEventListener`、`dispatchEvent`、`dispatchResolve`、`checkDuplicateEvent`、`handleFrameworkEvent`、`handleFrameworkEventByType`、`handleSDKEvent`、`handleSocketServerEvent`、`setStore` |
| 宿主桥 | `postMessage`、`postMessageSync`、`receiveMessage`、`resolvePromise`、`addUWPListener`、`removeUWPListener` |
| 初始化 | `initialize`、`initFinally`、`setInitializeStatus`、`init`、`checkFWUpdate`、`restore` |
| 设备数据 | `getAuraSyncStatus`、`setAuraSync`、`setAuraUnSync`、`linkAuraSync`、`unlinkAuraSync`、`getMacro`、`getPowerInfo`、`switchProfile`、`notifyDeviceProfileChange` |
| 页面控制 | `queryPage`、`getHTMLPage`、`setTitleBar`、`setTaskbarModelName`、`showUWPBackIcon`、`showUWPIcon`、`showSetting`、`showDialog`、`clearWebview`、`launchTrayIcon`、`broadcastTrayIcon`、`lockMenu` |
| 系统/文件 | `getDisplayName`、`getCurrentLanguage`、`getCountryCode`、`getFolderPath`、`copyFile`、`exportFile`、`getFileDetail`、`getImageDetail`、`openWebPage`、`usbRescan`、`rePluginDevice`、`reboot`、`switchApp` |
| 灯效生态 | `getAuraMode`、`queryAuraEffect`、`auraRescan`、`getAuraCreator`、`launchAuraCreator`、`queryMatrixEditor`、`mbLEDChange`、`queryMode` |
| 同步/省电 | `switchWDLSync`、`syncWDLControlStatus`、`checkBroadcastPower`、`checkHPCBroadcast`、`checkSeamlessQueue` |

**「页面 → 本地服务」的实际调用路径**（由代码结构确定）：

1. 页面 `index.js` 用 `composeURL(baseUrl + "/buttons") + sendRequest(...)` 发 HTTP（GET/POST/PUT/DELETE，`responseFormat:"json"`，超时 6e4 ms）。
2. 框架 express 收到后转给设备模块（`modules\mouse|unifyDongle\index.js`），模块把 JSON 转 XML（`parseJSON2XML`）后用 `send({method:"post",deviceType,sessionKey,xml})` 交给 SDK 侧。
3. 反向事件：`role=devicePage` 的 WS 推送 `{cmd,id,msg}`；页面在 `onmessage` 里 `switch(cmd)` 分发（`DEVICE_PLUGGED`/`DEVICE_UNPLUGGED`/`DATA_COLLECTION_CLEARED`/`SYSTEM_RESUME_S3`/`RESET_ALL_EVENT`/`WDLSyncEvent`…）。
4. service.js 以 `role=deviceService` 常连，处理框架下发的命令并回 `sendSocketServer({xml:{command:…,result:…}})`。

### 10.6 命令表（socket 消息里的 `command` / `cmd`）

`command`（框架→服务，19 个）：`writeLog, GetDeviceProfileList, GetDeviceProfile, GetDeviceDescription, IsNewDevice, broadcastEvent, GetAuraSyncState, DeviceProfileChange, ConnectionOpen, pid, SetDeviceProfile, UpdateWindowsLightingState, UnAuraSync, AuraSync, GetMacroList, ShowToast, GetDeviceLightingControl, WSDSyncStatus(相关), deviceServiceFinished`。

`cmd`（双向事件，27 个）：`log, GetAuraSyncState, getAuraMode, getCurrentLanguage, getArmouryCrateType, linkAuraSync, unlinkAuraSync, exportFile, auraRescan, switchApp, mbLEDChange, rePluginDevice, getDisplayName, getFileDetail, getImageDetail, getAuraCreator, queryMode, getFolderPath, copyFile, queryAuraEffect, getAppList, getACVersion, queryMatrixEditor, getCountryCode, syncWDLControlStatus, WDLSyncEvent, syncWDLControlEvent, GetMacroList, CHECK_DEVICESERVICE_EXIST, GET_LIGHTING_SYNC_STATUS`。

主进程额外识别的 WS 帧：`broadcastEvent`、`writeLog`（`level` 2/3/4/5 → error/warn/info/debug）、`socketServerEvent`、`AcDiagnosisChange`；其中 `cmd:"10101"` 特判为 `deviceServiceFinished`。

### 10.7 错误码（service.js 与主进程各持一份）

| 段 | 含义 |
|---|---|
| `-1` | UNKNOWN |
| `1000–1006` | SDK_DEVICE_NOT_FOUND / PROCESS_CRASHED / PROCESS_REBUILD / RETURN_FAILED / DEVICE_NOTALIVE / CONFIG_NOT_FOUND / DEVICE_NOTSERVICES |
| `1010–1015+` | CAPS_NOT_FOUND / FILE_NOT_FOUND / WRITE_FILE_FAILED / FOLDER_NOT_DEFINED / FORIGN_LIB_NOT_FOUND / SDK_FILE_NOT_FOUND / PLUGIN_FORMAT_ERROR |
| `4001–4005` | ICAFEMODE / AURASYNC / DEVICEISALIVE / BOOTLOADERMODE / WEBAPP |
| `5001–5003` | GET_AURA_FAILED / GET_SHORTCUT_FAILED / GET_ISAPPEXIST |
| `20000/20101/20102` | WEBSOCKET_TARGET_NOT_FOUND / POWER_INFO / POWER_TRAY_ICON |

主进程版本带 `S-` 前缀（`S-1000`…`S-1008`，含 `MAINSDK_NOT_READY`、`FOREIGN_LIB_NOT_FOUND`、`SDK_INIT_ING`），用于跨进程错误透传。

## 11. 页面 ↔ 本地服务 ↔ SDK 调用链

~~~~
┌─────────────────────────────── Electron (asus_framework.exe, app.asar 主进程) ───────────────────────────────┐
│  BrowserView #1  titleBar  file://…/renderer/titleBar/index.html                                            │
│  BrowserView #2  window    http://127.0.0.1:1042/<PID>/index.html?isElectron=1&session=<uuid>               │
│      └── preload.js → window.frameworkAPI.{webview|electron}.{postMessage,postMessageSync,subscribeEvent}    │
│                                                                                                              │
│  express + ws  (127.0.0.1:1042 / https 1043)                                                                │
│    GET|POST /:resource?                                    → devices/fonts/language/view/window/install/app/process
│    GET|POST|PUT|DELETE /type/:deviceType/model/:modelNumber/:resource[/:id]                                 │
│    POST /type/…/file,  GET|POST|… /lib/:version/type/…                                                       │
│    WS  ?role=home | devicePage | deviceService | devicePageLogger                                           │
│         (ws://127.0.0.1:9013 alertSocketServer, ws://127.0.0.1:9014 logSocketServer)                        │
│                                                                                                              │
│  modules/mouse|unifyDongle/index.js                                                          ← 设备模块       │
│    JSON → parseJSON2XML → send({method:"post", deviceType, sessionKey, xml})                                 │
└──────────────────────────────────────────────┬───────────────────────────────────────────────────────────────┘
                                               │  XML: <root><device_type key="1"><device key="6871">
                                               │        <function key="1|2|3|4|…"> / <data_name key="2">
                                               ▼
                        MainSDK 宿主（进程/端口未验证）→ ArmouryMsSDK.dll
                        ExecuteFunction(LPCWSTR xml, IntPtr* pOut)
                                               ▼
                        AacMouseHal_x86.dll  帧 51 31 <子命令> 00 + 65 字节报告
                                               ▼
                        ROG STRIX IMPACT III WIRELESS (P520) / 2.4G Dongle
                                               
      ┌──────────────── 设备服务进程（每 PID 一个） ────────────────┐
      │  View/<PID>/service.js  ← utilityProcess.fork(host=…, …)   │
      │    ├─ WS  client  role=deviceService  → 框架               │
      │    ├─ HTTP GET/POST  /file, /reset, /initialize/devicePage │
      │    ├─ 读写 C:\ProgramData\ASUS\ArmourySDK\MouseSDK\P520\<deviceSN>\{button,performance,effect,calibration}<n>.xml │
      │    └─ 常量表 FUNCTION_NUMBER / DATA_NUMBER / SOCKET_EVENT   │
      └────────────────────────────────────────────────────────────┘
~~~~

调用链文字版（一次「改 DPI」）：

1. 用户拖动 DPI 滑杆 → 页面 dispatch → `setPerformance`（6871 index.js 日志标签）。
2. 页面 `POST http://127.0.0.1:1042/api/type/1/model/6871/performance`（`composeURL(caps.url + "/performance")`）。
3. express 路由 `/type/:deviceType/model/:modelNumber/:resource` → `modules/mouse/index.js` 的 `setPerformance`。
4. 模块生成 XML（`function.key = 1 (PERFORMANCE)` / `data_name.key = 2 (PERFORMANCE_INFO)`，`dpi_1..4`、`polling_rate`…）→ `send(...)`。
5. SDK 侧 `ExecuteFunction` 下发到 Hal，HID 帧 `51 31 …` 写入设备；同时服务层把结果写进 `performance<n>.xml`。
6. 回程：deviceService 或设备模块 `broadcastEvent({target:{role:["devicePage"],…},msg:{cmd,…}})` → 页面 WS 收到 → Redux 更新 UI。

## 12. 未验证项与边界

| # | 项 | 状态 |
|---|---|---|
| 1 | 6873 蓝牙页为何未安装到 `View`（安装包内完整、p520 已登记其卸载 GUID） | 未验证（推断：按连接方式分组件安装，本次只装了 2.4G 组件） |
| 2 | `modules/mouse:send({method:"post",…,xml})` 的目标进程/端口（MainSDK 宿主） | 未验证（属 SDK/驱动文档范围） |
| 3 | `ExecuteFunction` 的实际调用点（app.asar 全部文本中 0 次） | 未验证（可能在 `.sign/addon/ac_node_addon.node` 或 MainSDK 进程内） |
| 4 | `.sign` 目录的校验算法与触发时机 | 未验证（仅确认 6 个条目与主进程 99 处引用） |
| 5 | `role=deviceService` 的 WS 服务端归属（主进程里未出现 `devicePageLogger` 字样，可能由 SDK 侧模块注册） | 未验证 |
| 6 | service.js 内嵌 caps 与 `_ref/caps.json` 是否逐字段相等（键集已核对一致，值未做 diff） | 部分验证 |
| 7 | 989/606 两个大分包的具体业务归属（依据体积与 `className: statics/breathing/reactive` 推断） | 未验证 |
| 8 | 6862/omni 的 MD5.ini `Count` 与目录文件数的差值（120 vs 123、82 vs 89） | 未验证（推断：排除 config/index/MD5 与 HomePage.exe 等非 web 资产） |

### 方法备注

- 全部通过 PowerShell `Get-ChildItem/Get-Content` 与 Node `fs.readFileSync` + 正则统计完成，**未执行任何 JS/exe**，未调用 `mcp__rea__*`。
- asar 因 Electron 对 `.asar` 路径的 fs 拦截，先 `Copy-Item` 为 `tmp-app.bin`（工作区临时副本）再按 pickle 头自解析，分析后删除。
- 字符串计数均为「子串出现次数」，包含压缩后同一常量在模板字符串中的重复拼接，仅用于横向比较，不代表语义调用次数。




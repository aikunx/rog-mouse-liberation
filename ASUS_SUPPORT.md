# ASUS / ROG 鼠标支持（本次新增）

> 目标设备：**ROG STRIX IMPACT III WIRELESS (P520)**，Omni 接收器 VID 0x0B05 / PID 0x1ACE。
> 实现方式：HID 私有协议直连，**不依赖 Armoury Crate / ArmouryDevice / 任何 ASUS 服务**。

## 1. 改动清单

| 类型 | 文件 | 说明 |
|---|---|---|
| 新增 | src/Hardware/AsusDeviceHelper.cs | ASUS 鼠标 HID 协议实现（枚举、读电量、读/写轮询率、设 DPI、切档） |
| 修改 | src/Hardware/DeviceManager.cs | QueryAllDevices() 新增 2.8 Query ASUS / ROG 分支 |
| 产物 | FerrisPulse.exe | 重新编译（696,320 字节，无新增依赖） |

未改动任何既有品牌逻辑、UI 控件或构建脚本。

## 2. 协议实现要点

~~~
包格式：64 字节，[0]=reportId(0x03, P520 专用), [1]=命令族, [2]=子命令, [5..]=载荷
控制接口：HID#VID_0B05&PID_1ACE&MI_02&Col03（usage_page 0xFF01）
~~~

| 功能 | 报文 | 解析 |
|---|---|---|
| 读电量 | 03 12 07 | [1]==0x12 && [2]==0x07 → [5]=百分比，[10]>0=充电，[6]=省电，[7]=低电阈值 |
| 读轮询率 | 03 12 04 00 | [13] = 0/1/2/3/4/5/6 → 125/250/500/1000/2000/4000/8000 Hz |
| 设轮询率 | 03 51 31 04 00 rate | 回显 [1]==0x51 && [2]==0x31 即成功 |
| 设某档 DPI | 03 51 31 (stage-1) 00 lo hi R G B | encoded=(DPI-50)/50，小端 16 位 |
| 切 DPI 档 | 03 51 31 09 00 stage | |
| 读 DPI 档 | 03 12 04 02 | [5..] 为各档 encoded 值（X/Y 成对） |

底层 I/O 采用与 VgnDeviceHelper 一致的 OVERLAPPED + 事件等待模式（先 drain 残留报告，再写，再读），避免阻塞 UI 线程。

## 3. 实测验证（本机）

~~~
before : 42% / 1000Hz
set 500 : True
after  : 500Hz
restore 1000 : True
final  : 42% / 1000Hz
~~~

- 电量 42% 与 ArmouryDevice SDK 日志中的 GetPowerStatus ... 42 完全一致
- 轮询率写入→读回确认闭环成立，且已恢复原值
- 通过 DeviceManager.QueryAllDevices() 验证集成：

~~~
devices: 1
  [ASUS] ROG STRIX IMPACT III WIRELESS | 42% | 1000Hz | Mouse | id=asus_rog_strix_impact_iii_wireless
~~~

## 4. 对外 API

~~~csharp
// 读取
MouseBatteryInfo d = AsusDeviceHelper.QueryAsusDeviceInfo();
ushort[] binds   = AsusDeviceHelper.GetButtonBindings();      // 8 槽位动作码
byte[]  light    = AsusDeviceHelper.GetLightingRaw(1);        // 12 字节灯效

// 写操作（均已实测）
AsusDeviceHelper.SetPollingRate(500);                         // 125..8000
AsusDeviceHelper.SetDpiStage(1, 1600, 255, 0, 0);            // 档 1 = 1600 DPI 红
AsusDeviceHelper.SwitchDpiStage(2);
AsusDeviceHelper.SetButtonBinding(4, 0x00F6);                // 侧前进 -> 音量加
AsusDeviceHelper.SetLighting(0, 25, 255, 0, 0);              // 静态红，亮度 25
~~~

UI 侧无需改动：设备以 Brand="ASUS"、Category=Mouse 出现在现有列表，电量与轮询率字段直接生效；缓存位于 HKCU\Software\RazerBatteryTray\AsusCache。

## 5. 宏：设备侧不支持（逆向结论）

用 REA/Ghidra 反编译 ArmouryMsSDK 后确认：**宏不在鼠标固件里**。

- `ParseKeyChangeXml` 中 `target_key_1 == 4` 表示「此键触发宏」，仅是一个标记
- 字符串 `macrokey_uuid` 的唯一引用函数第一条语句就是 `FindWindowW(L"AsusDialAgent", NULL)`
- `SetKeyChange`（key=0）伪代码只有 `dexin index/source/target` 与 RapidFire 延迟校验，无宏载荷
- 设备按键表读回只有 8 个 2 字节动作码，没有可变长数据区

即：宏序列由主机侧宏引擎（Armoury Crate / AsusDialAgent）执行，HID 协议中没有写入宏的通道。

若 FerrisPulse 要支持宏，只能自建引擎：低级键盘钩子捕获 → SendInput 回放 → 宏定义存本地。注意模拟输入存在反作弊风险。

## 6. 尚未完成

1. **多型号**：目前只登记 P520（0x1ACE），可扩展 P518（0x1A88 / mi_00）等。
2. **DPI 读取**：`12 04 01` 本机返回全 0，需先选定 profile。
3. **UI 专属控件**：未新增按键/灯效/DIP 的界面，仅提供 API 与设备列表展示。
4. **宏引擎**：未实现（见第 5 节）。

## 7. 协议来源与许可

HID 协议格式源自 g-helper 项目（GPL v3）的逆向成果，属公开事实性信息；本仓库实现为独立重写。

## 8. 官方快捷方式功能对照（结论：不能完全替代）

两个桌面快捷方式（ROG 影刃 3 无线版 / ROG Omni 接收器）都指向 HomePage.exe，即官方 Gear 窗口的 p520 与 omni 页面。

| 官方功能 | 能否替代 | 现状 |
|---|---|---|
| Performance（DPI / 轮询率 / 角度吸附 / 角度微调 / 加速度） | ✅ 可 | 读写均实测 |
| Buttons（8 键重映射 / RapidFire） | ✅ 基本可 | 读写实测；宏与手势除外 |
| Lighting（灯效模式 / 亮度 / RGB） | ✅ 可 | 读写实测（P520 仅滚轮区） |
| Power（自动省电 / 低电阈值） | ✅ 可 | 读写实测 |
| Profile 1–5 切换 | ✅ 可 | 切换与读取实测 |
| 电量 / 充电状态 | ✅ 可 | 42% 与 SDK 日志一致 |
| DPI 档位读取 | ✅ 可 | 400/800/1600/3200 实测 |
| **宏（Macro）** | ❌ 不可 | 设备只存标记，执行在主机的 AsusDialAgent |
| **固件升级** | ❌ 未实现 | 需 MsFWUpdate + 固件文件 + 擦写流程 |
| **表面校准** | ❌ 不可 | SDK 把 calibration XML 写进 ProgramData，无 HID 命令 |
| 手势（gesture_key_1..3） | ❌ 未实现 | 字段已知，设备侧协议未解 |
| Promotion / 多语言 / 主题 | ❌ 不适用 | 纯 UI 内容，非设备能力 |

### 结论

- 日常调节（DPI、轮询率、按键、灯效、省电、Profile、电量）**可以完全脱离官方软件**。
- 宏、固件升级、表面校准**不能**用 HID 替代，原因分别是：宏在主机侧执行、固件需要专用烧写流程、校准是主机侧配置。

## 9. 新补齐的协议（本轮实测）

| 功能 | 报文 | 解析 |
|---|---|---|
| 当前 Profile | 03 12 00 | [11] = 1..5 |
| 切换 Profile | 03 50 02 profile | 回显 [1]==0x50 |
| DPI 档位 | 03 12 04 02 | 每档 4 字节（X/Y 各 2 字节），encoded=(DPI-50)/50 |
| 省电 + 低电 | 03 51 37 00 00 powerOff 00 lowBatt | 读回 12 07 的 [6]/[7] |
| 灯效读 | 03 12 03 zone | 12 字节 |

实测片段：

~~~
profile: 1 -> switch 2 -> read 2 -> back 1
dpi stages: 400, 800, 1600, 3200
power: off=0(1分钟) lowBatt=10
set poweroff=3min -> readback [6]=03 -> restore [6]=00
~~~
## 10. 宏引擎与 UI 面板（本轮新增）

| 类型 | 文件 | 说明 |
|---|---|---|
| 新增 | src/Services/MacroEngine.cs | 主机侧宏引擎：录制（WH_KEYBOARD_LL）、播放（SendInput 扫描码）、注册表持久化 |
| 新增 | src/UI/Forms/AsusMouseForm.cs | 独立设置面板：Device / Buttons / Macros 三页 |
| 修改 | src/UI/Forms/MainForm.Tray.cs | 托盘菜单新增「ROG 鼠标设置」入口 |
| 修改 | src/UI/Forms/MainForm.cs | 新增 asusItem 字段声明 |

### 宏引擎设计（对应官方 AsusDialAgent 的主机侧职责）

~~~
录制：SetWindowsHookEx(WH_KEYBOARD_LL) 捕获按键，按时间差自动插入 Delay 步骤
存储：HKCU\Software\RazerBatteryTray\Macros\<名称>  =  kind,scanCode,delayMs;...
回放：SendInput(KEYEVENTF_SCANCODE)，dwExtraInfo 打标记 'RPTC' 以识别自身注入
清理：停止录制时卸载钩子，避免长期驻留
~~~

注意：设备本身不存宏（见第 5 节），因此宏的触发是主机侧的——目前提供手动播放；
若要挂到鼠标侧键，需要额外把侧键绑定为某个热键再由 RegisterHotKey 捕获，属于下一步。

### UI 面板

- **Device**：电量/充电/轮询率、Profile 1–5 切换、轮询率下拉、灯效（模式/亮度/RGB）、自动省电
- **Buttons**：8 槽位列表（显示当前动作码），选中后从下拉框改绑并应用
- **Macros**：录制/停止、播放、删除，宏存注册表

未改动主窗体 81 KB 的自绘布局，面板以独立窗口呈现，风险隔离。
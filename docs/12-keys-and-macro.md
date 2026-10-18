# 12 · 按键重映射与宏（REA 逆向结论）

> 方法：g-helper 源码对照 + REA/Ghidra 对 ArmouryMsSDK.dll 的反编译 + 本机实测。
> 结论先行：**按键重映射设备侧完整支持；宏不在设备里，由主机代理执行。**

## 12.1 按键重映射协议（已实现并实测）

~~~
读：03 12 05                → 回包 [1]==0x12 && [2]==0x05，从 [5] 起每 2 字节一个小端动作码
写：03 51 21 00 00 src_lo src_hi dst_lo dst_hi
RapidFire：03 51 22 00 00 l_lo l_hi r_lo r_hi s_lo s_hi
~~~

8 个槽位（设备侧源码固定）：

| 槽 | 源码 | 默认 |
|---|---|---|
| 0 | 0x01F0 | 左键 |
| 1 | 0x01F1 | 右键 |
| 2 | 0x01F2 | 滚轮键 |
| 3 | 0x01E4 | 侧后退 |
| 4 | 0x01E5 | 侧前进 |
| 5 | 0x01E6 | DPI 键 |
| 6 | 0x01E8 | 滚上 |
| 7 | 0x01E9 | 滚下 |

目标动作码 = 标准 USB HID Usage ID（0x04=A … 0xE7=右 GUI）或媒体码（0x00F0=播放/暂停、0x00F4=静音、0x00F5/0x00F6=音量±、0x00F2/0x00F3=上下曲），另有 0x01C0/0x01C1 = RapidFire。

### 本机实测

~~~
before slot4: 0x01E5 Forward
set slot4 -> Volume Up: True
after  slot4: 0x00F6 Volume Up
restore -> Forward: True
final  slot4: 0x01E5 Forward
~~~

八槽读取返回全部默认值，写入后读回确认生效，随后恢复原值。

## 12.2 宏的逆向结论

### 证据链

1. ArmouryMsSDK 的 `ParseKeyChangeXml` 解析字段：`source_key`、`target_key_1`…`target_key_4`、`gesture_key_1..3`、`iDirection`、`iGestureFunction`。
2. 反编译 `FUN_1002ed23`（ParseKeyChangeXml 本体）发现：当 `target_key_1` 解析出的数值 **== 4** 时，走 `[ArmouryMsSDK][ParseKeyChangeXml][Macro]` 分支；`== 7` 走另一分支。
3. 字符串 `macrokey`、`macrokey_uuid`、`dial_uuid` 的唯一引用位于 `FUN_10038d8a`，该函数第一行是：
   `hWnd = FindWindowW(L"AsusDialAgent", NULL);`
   —— 即宏与拨盘数据通过**外部进程 AsusDialAgent** 的窗口消息传递，而不是 HID 命令。
4. `SetKeyChange`（key=0）伪代码里只有 `dexin index/source/target` 与 RapidFire 延迟校验，没有任何宏序列的下发。
5. 设备侧按键表读回的只有 8 个 2 字节动作码，没有可变长数据区。

### 结论

| 问题 | 答案 |
|---|---|
| 宏存在鼠标固件里吗 | **不存**。设备只保存「哪个键触发哪个动作码」 |
| target_key=4 是什么 | 宏标记。它告知主机侧「这个键触发宏」，宏本身不在设备 |
| 宏序列在哪里执行 | 主机侧宏引擎（Armoury Crate / AsusDialAgent） |
| `macrokey_uuid` 是什么 | 宏库条目的标识，存在于主机配置文件，用于把按键与宏关联 |
| 能否用 HID 直接写入宏 | **不能**。协议里没有宏载荷通道 |

## 12.3 对 FerrisPulse 的含义

按键重映射可以完整直连实现（已完成）。若要让 FerrisPulse 也支持宏，只能**自建宏引擎**：
1. 用低级键盘钩子（WH_KEYBOARD_LL）或 HID 原始输入捕获触发键
2. 用 SendInput 回放按键序列（可带延迟）
3. 宏定义存本地（JSON/注册表），与设备按键绑定解耦
4. 注意：宏回放属于模拟输入，部分反作弊会检测，需在文档中明确风险

这与 g-helper 的做法一致——它有 RapidFire 与按键绑定，但没有宏引擎。

## 12.4 灯效（顺带补齐）

~~~
读：03 12 03 zone        → 12 字节 LightingSetting
写：03 51 28 zone 00 mode brightness R G B dir random speed
~~~

本机实测读取：`04 19 FF 00 00 …` → 模式 4（反应）、亮度 25、颜色红。P520 只有 Scrollwheel 区（zone=1）。

模式码：0 静态 / 1 呼吸 / 2 循环 / 3 彩虹 / 4 反应 / 5 彗星 / 6 电量 / 0xF0 关。

## 12.5 仍未闭合的点

1. `target_key_1 == 7` 分支的语义（推测为快捷方式/系统命令）未继续追。
2. `gesture_key_1..3` 的完整语义（手势多键组合）只看到字段与方向校验。
3. DPI 读取 `12 04 01` 仍返回全 0，需先选定 profile 才能读。
# 11 · ROG 鼠标 HID 协议（g-helper 实证，P520 完整命中）

> 来源：https://github.com/seerge/g-helper （GPL v3），`app/Peripherals/Mouse/`
> 关键文件：`AsusMouse.cs`（2,587 行，协议基类）、`Models/StrixImpactIIIWireless.cs`（P520 型号）
> **本文件补上了 09/10 章标为"未证实"的 HID 协议**

## 11.1 设备匹配（P520 精确命中）

```csharp
// Models/StrixImpactIIIWireless.cs — 注释即 //P520
public class StrixImpactIIIWirelessOmni : AsusMouse
{
    public StrixImpactIIIWirelessOmni()
        : base(0x0B05, 0x1ACE, "mi_02&col03", true, 0x03) { }
}
```

| 参数 | 值 | 与我们逆向的对照 |
|---|---|---|
| VID | 0x0B05 | 一致 |
| PID | 0x1ACE | 一致（Omni 接收器） |
| HID 接口 | `mi_02&col03` | 新增：接收器上的第 2 功能接口、第 3 集合 |
| wireless | true | 一致 |
| reportId | **0x03** | 新增：报文首字节 |
| 包长 | 64 字节（`USBPacketSize()`） | 与原记录「65 字节（ReportID+64）」略有差异，见 11.9 |
| DPI 档 | 4（`DPIProfileCount`） | 与 caps 一致 |
| Profile 数 | 5 | 与 caps 一致 |
| MaxDPI | 36000 | 一致 |
| 灯效区 | 仅 Scrollwheel | 一致（无 logo 灯） |
| 支持 | XY DPI、抬起距离、角度吸附/微调、自动省电、低电提示、DPI 颜色 | 与 caps 能力表对应 |

## 11.2 报文结构

~~~
[0]      reportId（P520 = 0x03；多数型号为 0x00）
[1]      0x51 = 写命令族 / 0x12 = 读命令族 / 0x50 = profile 切换
[2]      子命令
[3]      参数1（常为 profile 或 zone 索引）
[4]      参数2（常为 0x00）
[5..]    载荷
[..63]   补零到 64 字节
~~~

**这正是原始记录里的 `51 31 <子命令> 00`** —— 之前在三处二进制里搜不到常量，是因为官方 SDK 从不直接发 HID（走 XML→SDK→COM），只有第三方直连实现才暴露它。

## 11.3 完整命令表

### 读命令族（0x12）

| 报文 | 作用 | 响应解析 |
|---|---|---|
| `{rid, 0x12, 0x07}` | 读电量/电源状态 | 见 11.4 |
| `{rid, 0x12, 0x00}` | 读设备信息 | — |
| `{rid, 0x12, 0x03, zoneIdx}` | 读灯效 | 12 字节 LightingSetting |
| `{rid, 0x12, 0x04, 0x00}` | 读轮询率 | — |
| `{rid, 0x12, 0x04, 0x01}` | 读 DPI | — |

### 写命令族（0x51）

| 报文 | 作用 | 载荷说明 |
|---|---|---|
| `{rid, 0x51, 0x31, 0x04, 0x00, rate}` | 设轮询率 | rate: 0=125 / 1=250 / 2=500 / 3=1000 / 4=2000 / 5=4000 / 6=8000 Hz |
| `{rid, 0x51, 0x31, 0x05, 0x00, debounce}` | 设去抖 | 0=off, 1..7 = 8/12/16/20/24/28/32ms |
| `{rid, 0x51, 0x31, 0x06, 0x00, 0/1}` | 角度吸附 | 1=开 |
| `{rid, 0x51, 0x31, 0x07, 0x00, accel}` | 加速 | |
| `{rid, 0x51, 0x31, 0x08, 0x00, decel}` | 减速 | |
| `{rid, 0x51, 0x31, 0x09, 0x00, profile}` | 切换 DPI 档 | profile 从 1 开始 |
| `{rid, 0x51, 0x31, 0x0A, 0x00, count}` | 设 DPI 档数量 | P520 范围 1..4 |
| `{rid, 0x51, 0x31, 0x0B, 0x00, lo, hi}` | 角度微调 | int16 小端，步进 5（P520） |
| `{rid, 0x51, 0x31, 0x12, 0x00, 0/1}` | 启用/禁用设备 | |
| `{rid, 0x51, 0x31, N-1, 0x00, lo, hi, R, G, B}` | **设第 N 档 DPI** | 子命令 = profile-1；DPI 编码见 11.5；后 3 字节是该档颜色（支持 DPI 颜色的型号） |
| `{rid, 0x51, 0x35, 0xFF, 0x00, 0xFF, liftOff}` | 抬起距离 | 0=Low / 1=High |
| `{rid, 0x51, 0x37, 0x00, 0x00, powerOff, 0x00, lowBatt}` | 自动省电 + 低电提示 | powerOff: 0/1/2/3/4 = 1/2/3/5/10 分钟，0xFF=永不；lowBatt 为百分比 |
| `{rid, 0x51, 0x28, zone, 0x00, mode, bright, R, G, B, dir, random, speed}` | 设灯效 | mode: 0 静态 / 1 呼吸 / 2 循环 / 3 彩虹 / 4 反应 / 5 彗星 / 6 电量 / 0xF0 关 |
| `{rid, 0x50, 0x02, profile}` | 切换 Profile | |

## 11.4 电量、充电、省电的响应解析

```csharp
if (packet[1] == 0x12 && packet[2] == 0x07) {
    Battery       = packet[5];          // 百分比 0-100，直接是百分数（非 0-255）
    Charging      = packet[10] > 0;
    PowerOff      = (PowerOffSetting)packet[6];
    LowBattWarn   = packet[7];
}
```

**重要差异**：雷蛇的电量是 0–255 原始值需换算，ASUS 这里 `packet[5]` **直接是百分比**。
我们的 SDK 日志里 `GetPowerStatus_AutoResponseEvent exit 42` 返回 42，与此吻合（42%）。

## 11.5 DPI 编码（修正我们的公式）

```csharp
ushort dpiEncoded = (ushort)((dpi.DPI - DPIIncrements()) / DPIIncrements());   // 编码
DPI = (encoded) * DPIIncrements() + DPIIncrements();                          // 解码
// DPIIncrements() 默认 50，MinDPI 100，P520 MaxDPI 36000
```

即 `encoded = (DPI - 50) / 50`：400→7、800→15、1600→31、3200→63 —— **与既有文档的 `(dpi-100)/50+1` 完全等价**，两条都成立。

## 11.6 底层 I/O

`PeripheralEventListener` 用 `HidStream` 打开该 VID/PID 的**所有**匹配接口（不只是控制接口），在一个循环里读事件报告：

- 写：`WriteForResponse(packet)` → 先 `Drain()` 清残留 → `Write` → `Read` → 校验
- 错误约定：响应为 `FF AA` 表示「该固件不支持此命令」
- 空包 → 停止读取
- 读长度默认为 3 字节前缀匹配（`matchLength`）
- 每个操作前有 `FlushSettings()`，保证配置落地

设备就绪判定：`SetDeviceReady(Battery > 0)` —— 电量读到 0 视为休眠/离线。

## 11.7 型号覆盖（可作为对照表）

31 个型号：Balteus、BulwarkDock、Chakram、ChakramCore、ChakramX、GladiusII、GladiusIIIAimpoint、GladiusIIIWireless、GladiusIIWireless、HarpeAceAimLab、HarpeAceMini、HarpeIIAce、KerisIIAce、KerisIIOrigin、KerisWireless、KerisWirelssAimpoint、MD200、Pugio、PugioII、SpathaX、StrixCarry、StrixEvolve、StrixImpact、StrixImpactII、**StrixImpactIII**、**StrixImpactIIIWireless**、StrixImpactIIWireless、TUFM3、TUFM4Air、TUFM4Wireless、TUFM5。

P520 对应 `StrixImpactIIIWirelessOmni`；有线版 P518 是 `StrixImpactIII`（VID 0x0B05 / PID 0x1A88 / `mi_00` / reportId 0x00）。

## 11.8 与我们逆向的对照结论

| 项 | 之前结论 | 现在 |
|---|---|---|
| `51 31` 帧头 | 三处二进制无常量，判为「未证实/属 MouseHal」 | **证实存在**，是第三方直连的固定帧；官方 SDK 不发它 |
| HID 报告长度 | 65 字节（ReportID+64） | 实际 64 字节包，首字节即 reportId（0x03），写入时补零到 64 |
| 电量读取 | 只能经 SDK key=6 | 可直接 `0x12 0x07` 读 `packet[5]` |
| DPI 编码 | (dpi-100)/50+1 | 等价于 (dpi-50)/50，两条都对 |
| 设备接口 | 未知 | 接收器 `mi_02&col03` |

## 11.9 需要注意的差异

1. **包长**：g-helper 用 64 字节包（含 reportId），原记录写「65 字节（ReportID + 64 载荷）」。两种描述可能对应不同 API（`WriteFile` 带前缀 vs `HidD_SetFeature` 不带）。抓包时应以实际写入长度为准。
2. **P520 的 reportId 是 0x03**，不是默认 0x00。若直接用 0x00 发命令，P520 不会响应。
3. **接口选择**：必须打开 `mi_02&col03`，打开错误接口会读到空包。

## 11.10 可直接落地的下一步

有了这份协议，可以完全绕开 ArmouryDevice 做一个 P520 控制器：

1. 用 SetupDi 枚举 VID 0x0B05 / PID 0x1ACE，匹配路径含 `mi_02&col03`
2. `CreateFile` 打开，包长 64，首字节 0x03
3. 读电量：发 `03 12 07` + 补零 → 回包 `[1]==0x12 && [2]==0x07` → `[5]` 即百分比
4. 设 DPI 档 N：`03 51 31 (N-1) 00 lo hi R G B`，其中 `lo/hi = (DPI-50)/50` 小端
5. 设轮询率：`03 51 31 04 00 rate`
6. 设灯效：`03 51 28 zone 00 mode bright R G B dir random speed`

> 许可提示：g-helper 是 GPL v3。照抄代码需遵守 GPL；只借鉴协议格式（帧结构、命令码）属于事实性信息，不构成代码复制。

# ROG 影刃 3 无线版 · 逆向与控制端

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)
![platform Windows](https://img.shields.io/badge/platform-Windows-0078D6?style=flat-square&logo=windows&logoColor=white) ![C# WinForms](https://img.shields.io/badge/C%23-WinForms-239120?style=flat-square&logo=csharp&logoColor=white) ![polling 8000 Hz](https://img.shields.io/badge/polling-8000_Hz-orange?style=flat-square) ![protocol HID reverse engineered](https://img.shields.io/badge/protocol-HID_reverse_engineered-blueviolet?style=flat-square) ![license GPL v3](https://img.shields.io/badge/license-GPL_v3-orange?style=flat-square)

[**简体中文**](README.md) | [English](README.en.md)

---

## 📑 目录

- [🔥 这是什么？](#-这是什么)
- [⚡ 30 秒快速开始](#-30-秒快速开始)
- [🎯 能做什么、不能做什么](#-能做什么不能做什么)
- [🔬 最核心的发现](#-最核心的发现)
- [📚 文档](#-文档)
- [❓ 常见问题](#-常见问题)
- [🙏 致谢](#-致谢)
- [🤖 关于本项目](#-关于本项目)
- [⚠️ 免责声明](#️-免责声明)

---

## 🔥 这是什么？

**这是一次完整的技术解剖，也是一件可以立刻上手的替代品。**

我们把 ROG 影刃 3 无线版（P520）的 HID 私有协议**逐字节拆开**，写成 12 篇可复现的逆向文档；再基于这份协议，重写了一个**不依赖任何华硕软件**的原生控制端。

### 一句话说清价值

| 对比项 | 官方 Armoury Crate | 本项目 |
|---|---|---|
| 磁盘占用 | **370 MB** | **0.7 MB** |
| 常驻内存 | 10+ 进程，数百 MB | **单进程 ~40 MB** |
| 依赖 | Electron + Chromium + 10 个服务 | **纯 Win32 API，零依赖** |
| 卸载后 | 鼠标功能全废 | **完全自由** |

> 你买的是一只鼠标，不是一份全家桶订阅。

> 适用设备：ROG STRIX IMPACT III WIRELESS（P520），Omni 无线接收器
> 其他 ROG 鼠标理论兼容，但未测试

---

## 30 秒快速开始

### 我只想用，不想看代码

1. 下载 [Releases](https://github.com/aikunx/rog-mouse-liberation/releases/latest) 里的 `FerrisPulse.exe`
2. **右键 → 以管理员身份运行**（必须，否则读不到鼠标）
3. 右下角托盘出现图标，右键 →「ROG 鼠标设置」

就这三步。不需要装任何驱动、运行库、官方软件。

### 我想编译

```powershell
csc /target:winexe /optimize+ /win32icon:app.ico ^
    /r:System.Drawing.dll /r:System.Windows.Forms.dll ^
    /out:FerrisPulse.exe /recurse:src\*.cs
```

Windows 自带 `csc.exe`，不用装 Visual Studio。

### 我想看它是怎么逆向出来的

从 [文档目录](docs/00-index.md) 开始，或者直接看 [HID 协议那一篇](docs/11-asus-hid-protocol.md)。

---

## 能做什么、不能做什么

### ✅ 可以，而且不需要官方软件

| 功能 | 说明 |
|---|---|
| 看电量 / 充电状态 | 和官方软件读数一致 |
| 调 DPI（4 档） | 每档可以配不同颜色 |
| 调轮询率 | 125 / 250 / 500 / 1000 / 2000 / 4000 / 8000 Hz |
| 改按键功能 | 8 个按键槽位都能重映射 |
| 调灯效 | 模式、亮度、颜色、方向、速度 |
| 鼠标垫适配 | 角度吸附、角度微调、抬起距离、按键去抖 |
| 省电设置 | 闲置多久降频、低电量提醒阈值 |
| 板载配置 | 鼠标内置 5 组配置，切换即生效（换电脑也带着走） |
| 宏 | 录制、回放、绑定到侧键 |

### ❌ 做不到（需要官方软件或硬件层支持）

| 功能 | 为什么 |
|---|---|
| 固件升级 | 需要借助 bootloader 烧录，刷错会变砖，不做 |
| 表面校准 | 官方只是把参数写进本地文件，设备端没有对应命令 |
| 手势操作 | 鼠标硬件只上报单击事件，不产生手势数据 |
| Aura 灯效同步 | 需要华硕主板的总线支持 |

---

## 最核心的发现

如果只记住一件事：

> **每次写入设置后，必须发 `03 50 03` 保存命令，否则鼠标只回显、不保存。**

这是这个项目最大的坑。灯效改了没反应、DPI 设置不生效、重启就丢，99% 都是漏了这条。

### 协议长什么样

鼠标通过 HID 接收 64 字节的报文，格式固定：

```
第 0 字节     03          报告 ID（固定）
第 1 字节     51 / 12     51 = 写，12 = 读，50 = 配置操作
第 2 字节     xx          具体子命令
第 3-4 字节   xx xx       参数
第 5 字节起   ...         数据
```

一个完整的例子——把第 1 档 DPI 设成 400：

```
03 51 31 00 00 07 00 FF 00 00 ...（补零到 64 字节）
03 50 03                                          ← 保存
```

`07 00` 是编码后的 400（算法：`(DPI - 50) / 50`）。

### 常用命令速查

| 想做什么 | 发送 |
|---|---|
| 看电量 | `03 12 07` |
| 看当前配置 | `03 12 00` |
| 看 DPI 四档 | `03 12 04 02` |
| 看按键映射 | `03 12 05` |
| 看灯效 | `03 12 03 00` |
| 改某档 DPI | `03 51 31 (档位-1) 00 低字节 高字节 R G B` |
| 改轮询率 | `03 51 31 04 00 代码`（0=125, 1=250, 2=500, 3=1000, 4=2000, 5=4000, 6=8000） |
| 改按键 | `03 51 21 00 00 原键码 目标键码` |
| 改灯效 | `03 51 28 区域 00 模式 亮度 R G B 方向 随机 速度` |
| 切换配置组 | `03 50 02 组号`（1-5） |
| **保存** | `03 50 03` |

### 8 个按键的编号

| 号码 | 按键 | 默认键码 |
|---|---|---|
| 0 | 左键 | `01F0` |
| 1 | 右键 | `01F1` |
| 2 | 滚轮 | `01F2` |
| 3 | 侧后退 | `01E4` |
| 4 | 侧前进 | `01E5` |
| 5 | DPI 键 | `01E6` |
| 6 | 滚上 | `01E8` |
| 7 | 滚下 | `01E9` |

目标键码用标准 USB HID 码（`04`=A、`05`=B…），媒体键是 `F0`=播放、`F4`=静音、`F5/F6`=音量加减。

---

## 文档

12 篇，从安装包结构一路讲到 HID 协议：

| # | 文档 | 内容 |
|---|---|---|
| 00 | [索引](docs/00-index.md) | 从哪里开始看 |
| 01 | [安装包结构](docs/01-installer.md) | 6 个分包分别装什么 |
| 02 | [安装后目录](docs/02-installed.md) | 372 MB 都装在哪 |
| 03 | [页面层](docs/03-pages.md) | 4 个网页版配置页 |
| 04 | [二进制结构](docs/04-binaries.md) | 23 个可执行文件干什么 |
| 05 | [运行时架构](docs/05-architecture.md) | 进程、服务、通信链路 |
| 06 | [功能对照](docs/06-features.md) | 每个功能对应哪段代码 |
| 07 | [主进程](docs/07-main-process.md) | 从 source map 还原的 TS |
| 08 | [页面实现](docs/08-pages-code.md) | 页面业务逻辑 |
| 09 | [设备发现](docs/09-device-discovery.md) | 怎么识别鼠标 |
| 10 | [动态分析](docs/10-dynamic-analysis.md) | 抓包、解包 |
| 11 | [HID 协议](docs/11-asus-hid-protocol.md) | **最核心的一篇** |
| 12 | [按键与宏](docs/12-keys-and-macro.md) | 为什么宏必须主机侧执行 |

---

## 常见问题

**Q：会不会和官方软件冲突？**
A：会。两个程序抢同一个 HID 接口，建议只留一个。我们的程序不需要官方软件，可以直接卸载。

**Q：会不会被游戏反作弊封号？**
A：不会。我们只通过标准 HID 接口读写配置，不发输入事件、不注入进程、不读写游戏内存。宏功能使用 `SendInput` 模拟按键，和 AutoHotkey 原理相同——部分竞技游戏可能会检测这类工具，请自行判断是否使用。

**Q：支持 Mac / Linux 吗？**
A：不支持。这是 Windows 程序。

**Q：我的鼠标型号不在列表里能用吗？**
A：可能可以。协议族是华硕共用的，主要差异在 reportId（有的型号是 0x00）。改 `KnownDevices` 表里的参数试一试。

---

## 致谢

这个项目建立在别人的工作之上：

| 项目 | 作者 | 作用 |
|---|---|---|
| [FerrisPulse](https://github.com/Ferris-echo/FerrisPulse) | [@Ferris-echo](https://github.com/Ferris-echo) | 控制端基础框架 |
| [g-helper](https://github.com/seerge/g-helper) | [@seerge](https://github.com/seerge) | 协议交叉验证 |
| [acer-predator-rgb-gui](https://github.com/axtloss/acer-predator-rgb-gui) | [@axtloss](https://github.com/axtloss) | Linux 侧参考 |

**特别感谢 [DeepSeek](https://www.deepseek.com/)** 提供模型能力——这个项目的逆向分析、代码和文档，绝大部分由 AI 完成。

---

## 关于本项目

**本仓库的全部内容由 AI 辅助生成。**

| 项 | 说明 |
|---|---|
| 生成模型 | [DeepSeek](https://www.deepseek.com/) |
| 运行环境 | DeepSeek Harness + REA（Ghidra 后端） |
| 人的角色 | 提出目标、真机执行验证、反馈现象 |

所有协议命令、内存数字、功能边界都经过真机验证；但代码与文字确实由 AI 生成。请把它当作「AI 生成、人类验证」的产物。

---

## ⚠️ 免责声明

**使用本项目的任何代码、文档或方法，风险自负。**

1. **硬件风险**：修改 DPI、灯效、功耗、按键映射涉及与设备固件交互。操作不当（例如发送格式错误的 HID 报文）可能导致设备进入异常状态，需要重新插拔甚至复位才能恢复。
2. **保修**：卸载官方软件、修改设备设置可能影响厂商保修。是否违反保修条款，请咨询设备厂商。
3. **合法性**：本项目仅用于**你自己拥有的设备**。请勿用于修改他人设备、绕过计费、或在未经授权的环境中使用。
4. **无担保**：代码按「原样」提供，不提供任何明示或暗示的担保（包括但不限于适销性、特定用途适用性）。作者不对任何直接或间接损失负责。
5. **固件**：本项目**不做固件烧写**。如果你自行尝试刷写固件，风险完全由你承担——刷错会变砖。
6. **反作弊**：宏功能使用标准 `SendInput` 模拟按键，原理与 AutoHotkey 相同。部分竞技游戏可能将此类工具判定为违规，请自行判断是否使用。
7. **AI 生成**：本项目内容由 AI 生成、人类验证。虽然经过真机测试，但仍可能存在未发现的错误。执行任何命令前请先理解它的作用。

---
## 许可

源码基于 [FerrisPulse](https://github.com/Ferris-echo/FerrisPulse)（**GPL v3**），同样遵循 GPL v3。
逆向文档为原创分析，协议格式属公开事实性信息。

**免责**：修改硬件设置有风险，本项目不做固件烧写。

---

<p align="center">
  <i>让外设回归工具的本质</i>
</p>

# ROG Strix Impact III Wireless: Reverse Engineering + Standalone Control

> Freeing the ROG STRIX IMPACT III WIRELESS (P520) from the Armoury Crate ecosystem.
> Includes complete HID protocol documentation and a control app that does not require ASUS software.

[**English**](README.en.md) | [简体中文](README.md)

## What's inside

Three parts:

1. **Reverse engineering docs** (`docs/`) — 12 chapters, from installer to HID protocol
2. **Control app source** (`src/`) — ROG mouse software built on FerrisPulse
3. **Probe tools** (`tools/`) — HID protocol verification scripts

## Device

| Item | Value |
|---|---|
| Model | ROG STRIX IMPACT III WIRELESS (P520) |
| VID | 0x0B05 |
| PID | 0x1ACE (Omni receiver) |
| Control interface | `MI_02&Col03`, usage_page 0xFF01 |
| Packet | 64 bytes, reportId `0x03` |

## Key findings

### Protocol format

```
[0]    reportId 0x03
[1]    0x51 write / 0x12 read / 0x50 profile
[2]    sub-command
[3..]  payload
```

**Every write must be followed by `03 50 03`** to persist, otherwise the device only echoes.

### Verified capabilities

| Feature | Command |
|---|---|
| Read battery | `03 12 07` |
| Read device info | `03 12 00` |
| Read/Write DPI stages | `03 12 04 02` / `03 51 31 <stage-1> 00 lo hi R G B` |
| Read/Write polling rate | `03 12 04 00` / `03 51 31 04 00 rate` |
| Read/Write button mapping | `03 12 05` / `03 51 21 00 00 src dst` |
| Read/Write lighting | `03 12 03 00` / `03 51 28 zone 00 mode bright R G B dir random speed` |
| Switch profile | `03 50 02 n` (1–5) |
| Save | `03 50 03` |

### Button slots (actual device order)

| Slot | Button | Source code |
|---|---|---|
| 0 | Left | 0x01F0 |
| 1 | Right | 0x01F1 |
| 2 | Wheel click | 0x01F2 |
| 3 | Back | 0x01E4 |
| 4 | Forward | 0x01E5 |
| 5 | DPI | 0x01E6 |
| 6 | Scroll up | 0x01E8 |
| 7 | Scroll down | 0x01E9 |

### Onboard storage

The device stores 5 independent profiles; switching changes DPI / lighting / buttons together.

## Feature boundaries

| Capability | Depends on | Status |
|---|---|---|
| Battery / charging | Device | ✅ |
| DPI 4 stages RW | Device | ✅ |
| Polling rate 125–8000 | Device | ✅ |
| Button remap (8 slots) | Device | ✅ |
| Lighting (mode/brightness/RGB/direction/speed) | Device | ✅ |
| Angle snap / tuning / lift-off / debounce | Device | ✅ |
| Auto power-saving + low battery threshold | Device | ✅ |
| Onboard 5 profiles | Device | ✅ |
| Macros | Host-side engine | ✅ |
| Firmware update | Official | ❌ requires bootloader |
| Surface calibration | Official | ❌ official only writes XML |
| Gestures | Official | ❌ HID only emits single keys |

## Build

Requires .NET Framework 4.x (`csc.exe`).

```powershell
csc /target:winexe /optimize+ /win32icon:app.ico `
    /r:System.Drawing.dll /r:System.Windows.Forms.dll `
    /out:FerrisPulse.exe /recurse:src\*.cs
```

## Usage

1. Run as Administrator (HID access requires it)
2. Right-click tray icon → "ROG Mouse Settings"
3. First launch registers the HID control interface

## Relationship with official software

**Fully independent.** The device uses Windows' standard HID driver; it still works after uninstalling Armoury Crate.

During reverse engineering we found the official SDK never sends raw HID — that's why no frame constants appear in its binaries. The protocol was cross-verified with third-party direct-connect implementations (g-helper).

## Documentation index

| Doc | Content |
|---|---|
| `docs/00-index.md` | Index |
| `docs/01-installer.md` | Installer structure |
| `docs/02-installed.md` | Installed layout |
| `docs/03-pages.md` | Page layer |
| `docs/04-binaries.md` | Binary structure, function key dispatch |
| `docs/05-architecture.md` | Runtime architecture |
| `docs/06-features.md` | Feature mapping |
| `docs/07-main-process.md` | Main process (from source maps) |
| `docs/08-pages-code.md` | Page business logic |
| `docs/09-device-discovery.md` | Device discovery chain |
| `docs/10-dynamic-analysis.md` | Packet capture & unpacking |
| `docs/11-asus-hid-protocol.md` | HID protocol |
| `docs/12-keys-and-macro.md` | Keys & macro conclusions |

## Protocol sources

- Reverse engineering: this project
- Cross-validation: [g-helper](https://github.com/seerge/g-helper) (GPL v3)
- Linux reference: [acer-predator-rgb-gui](https://github.com/axtloss/acer-predator-rgb-gui)

## License

Source code is derived from [FerrisPulse](https://github.com/Ferris-echo/FerrisPulse) (GPL v3) and remains GPL v3.

Reverse engineering docs are original analysis; protocol formats are public factual information.

## Disclaimer

Modifying hardware settings (power, lighting, buttons) carries risk. No firmware flashing is provided.

---

[![](https://img.shields.io/badge/powered_by-dsh-4D6BFE?style=flat-square&logo=deepseek&logoColor=white)](https://github.com/deepseek-ai/deepseek-harness)

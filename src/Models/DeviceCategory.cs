// ============================================================================
// 模块名称: DeviceCategory.cs
// 所属区域: Models (数据契约层)
// 核心职责:
//   定义系统支持的外设分类枚举（鼠标、键盘、耳机、手柄、接收器等）
// 依赖关系: 无
//
// ⚠️ AI 维护准则 (AI MAINTENANCE DIRECTIVE - DO NOT REMOVE) ⚠️:
// 1. [契约同步更新]: 任何 AI Agent 在修改本文件逻辑或新增枚举项时，【必须】同步更新上方说明。
// 2. [C# 5 兼容铁律]: 严禁使用 C# 6+ 语法。
// 3. [编码格式锁定]: 本文件必须严格保存为【UTF-8 with BOM】编码。
// ============================================================================

namespace RazerBatteryTray
{
    public enum DeviceCategory
    {
        Mouse,
        Headset,
        Gamepad,
        Keyboard,
        Generic,
        Earbuds,
        Dongle
    }
}

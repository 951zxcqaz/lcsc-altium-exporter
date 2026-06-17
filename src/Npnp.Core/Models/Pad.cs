namespace Npnp.Core.Models;

/// <summary>
/// 焊盘形状枚举
/// </summary>
public enum PadShape
{
    Rect,
    Oval,
    Circle,
    RoundRect
}

/// <summary>
/// 焊盘层枚举
/// </summary>
public enum PadLayer
{
    Top,
    Bottom,
    MultiLayer
}

/// <summary>
/// 焊盘记录类型
/// </summary>
public record Pad(
    int Number,
    string Name,
    PadShape Shape,
    double X,
    double Y,
    double Width,
    double Height,
    double Rotation,
    PadLayer Layer);
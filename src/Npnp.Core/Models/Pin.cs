namespace Npnp.Core.Models;

/// <summary>
/// 引脚记录类型
/// </summary>
public record Pin(
    int Number,
    string Name,
    string Type,
    double X,
    double Y,
    double Rotation);
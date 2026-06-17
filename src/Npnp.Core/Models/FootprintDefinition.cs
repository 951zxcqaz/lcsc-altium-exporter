namespace Npnp.Core.Models;

/// <summary>
/// PCB封装定义记录类型
/// </summary>
public record FootprintDefinition(
    string Name,
    IReadOnlyList<Pad> Pads,
    IReadOnlyList<GraphicalElement> Graphics,
    Dimension? Body);
namespace Npnp.Core.Models;

/// <summary>
/// 原理图符号定义记录类型
/// </summary>
public record SymbolDefinition(
    string Name,
    IReadOnlyList<Pin> Pins,
    IReadOnlyList<GraphicalElement> Graphics);
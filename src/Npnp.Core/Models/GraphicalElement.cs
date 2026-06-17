namespace Npnp.Core.Models;

/// <summary>
/// 图形元素抽象基类
/// </summary>
public abstract record GraphicalElement(double X, double Y);

/// <summary>
/// 线段元素
/// </summary>
public sealed record LineElement(
    double X,
    double Y,
    double EndX,
    double EndY,
    double Width,
    string Layer) : GraphicalElement(X, Y);

/// <summary>
/// 圆弧元素
/// </summary>
public sealed record ArcElement(
    double X,
    double Y,
    double Radius,
    double StartAngle,
    double EndAngle,
    double Width,
    string Layer) : GraphicalElement(X, Y);

/// <summary>
/// 文本元素
/// </summary>
public sealed record TextElement(
    double X,
    double Y,
    string Content,
    double Height,
    double Rotation,
    string Layer) : GraphicalElement(X, Y);

/// <summary>
/// 矩形元素
/// </summary>
public sealed record RectElement(
    double X,
    double Y,
    double Width,
    double Height,
    double LineWidth,
    bool IsFilled,
    string Layer) : GraphicalElement(X, Y);
namespace Npnp.Core.Writers;

using System.Text;
using Npnp.Core.Models;

/// <summary>
/// SchLib 二进制文件写入器
/// </summary>
public class SchLibWriter : ISchLibWriter
{
    // 简化的文件魔数（实际 Altium 格式需要逆向工程确定）
    private static readonly byte[] MagicNumber = Encoding.ASCII.GetBytes("NPNP_SCHLIB_V1");

    /// <summary>
    /// 将元件写入到新的 SchLib 文件
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="components">要写入的元件集合</param>
    /// <param name="options">导出选项</param>
    public void Write(string outputPath, IEnumerable<ComponentDetail> components, ExportOptions options)
    {
        // 检查文件是否存在
        if (File.Exists(outputPath))
        {
            if (!options.ForceOverwrite)
            {
                throw new IOException($"文件 '{outputPath}' 已存在。使用 ForceOverwrite 选项覆盖。");
            }
        }

        // 确保目录存在
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        WriteHeader(writer, options);
        WriteComponents(writer, components);
        WriteFooter(writer);
    }

    /// <summary>
    /// 将元件追加到现有的 SchLib 文件
    /// </summary>
    /// <param name="outputPath">目标文件路径</param>
    /// <param name="components">要追加的元件集合</param>
    public void Append(string outputPath, IEnumerable<ComponentDetail> components)
    {
        throw new NotImplementedException("追加模式尚未实现。实际 Altium SchLib 格式需要逆向工程后才能实现追加功能。");
    }

    /// <summary>
    /// 写入文件头
    /// </summary>
    private void WriteHeader(BinaryWriter writer, ExportOptions options)
    {
        // 写入魔数
        writer.Write(MagicNumber);

        // 写入版本号（简化格式）
        writer.Write((ushort)1);

        // 写入库元数据
        WriteString(writer, options.LibraryName);
        WriteString(writer, DateTime.UtcNow.ToString("O"));
        WriteString(writer, "Npnp.Core SchLib Writer");
    }

    /// <summary>
    /// 写入所有元件
    /// </summary>
    private void WriteComponents(BinaryWriter writer, IEnumerable<ComponentDetail> components)
    {
        var componentList = components.ToList();

        // 写入元件数量
        writer.Write(componentList.Count);

        foreach (var component in componentList)
        {
            WriteSymbol(writer, component);
        }
    }

    /// <summary>
    /// 写入单个符号
    /// </summary>
    private void WriteSymbol(BinaryWriter writer, ComponentDetail component)
    {
        // 写入元件基本信息
        WriteString(writer, component.LcscId);
        WriteString(writer, component.Name);
        WriteString(writer, component.Description);

        // 写入符号定义
        if (component.Symbol != null)
        {
            writer.Write((byte)1); // 符号存在标志

            var symbol = component.Symbol;
            WriteString(writer, symbol.Name);

            // 写入引脚
            writer.Write(symbol.Pins.Count);
            foreach (var pin in symbol.Pins)
            {
                WritePin(writer, pin);
            }

            // 写入图形元素
            writer.Write(symbol.Graphics.Count);
            foreach (var graphic in symbol.Graphics)
            {
                WriteGraphic(writer, graphic);
            }
        }
        else
        {
            writer.Write((byte)0); // 符号不存在标志
        }
    }

    /// <summary>
    /// 写入引脚
    /// </summary>
    private void WritePin(BinaryWriter writer, Pin pin)
    {
        writer.Write(pin.Number);
        WriteString(writer, pin.Name);
        WriteString(writer, pin.Type);
        writer.Write(pin.X);
        writer.Write(pin.Y);
        writer.Write(pin.Rotation);
    }

    /// <summary>
    /// 写入图形元素
    /// </summary>
    private void WriteGraphic(BinaryWriter writer, GraphicalElement graphic)
    {
        switch (graphic)
        {
            case LineElement line:
                writer.Write((byte)1); // 线段类型标识
                writer.Write(line.X);
                writer.Write(line.Y);
                writer.Write(line.EndX);
                writer.Write(line.EndY);
                writer.Write(line.Width);
                WriteString(writer, line.Layer);
                break;

            case ArcElement arc:
                writer.Write((byte)2); // 圆弧类型标识
                writer.Write(arc.X);
                writer.Write(arc.Y);
                writer.Write(arc.Radius);
                writer.Write(arc.StartAngle);
                writer.Write(arc.EndAngle);
                writer.Write(arc.Width);
                WriteString(writer, arc.Layer);
                break;

            case TextElement text:
                writer.Write((byte)3); // 文本类型标识
                writer.Write(text.X);
                writer.Write(text.Y);
                WriteString(writer, text.Content);
                writer.Write(text.Height);
                writer.Write(text.Rotation);
                WriteString(writer, text.Layer);
                break;

            case RectElement rect:
                writer.Write((byte)4); // 矩形类型标识
                writer.Write(rect.X);
                writer.Write(rect.Y);
                writer.Write(rect.Width);
                writer.Write(rect.Height);
                writer.Write(rect.LineWidth);
                writer.Write(rect.IsFilled);
                WriteString(writer, rect.Layer);
                break;

            default:
                writer.Write((byte)0); // 未知类型
                break;
        }
    }

    /// <summary>
    /// 写入文件尾
    /// </summary>
    private void WriteFooter(BinaryWriter writer)
    {
        // 写入结束标记
        var endMarker = Encoding.ASCII.GetBytes("END");
        writer.Write(endMarker);

        // 写入校验和占位符（实际实现需要计算）
        writer.Write((uint)0);
    }

    /// <summary>
    /// 写入字符串（带长度前缀）
    /// </summary>
    private static void WriteString(BinaryWriter writer, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        writer.Write(bytes.Length);
        writer.Write(bytes);
    }
}
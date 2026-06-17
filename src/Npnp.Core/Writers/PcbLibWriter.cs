namespace Npnp.Core.Writers;

using System.Text;
using Npnp.Core.Models;

/// <summary>
/// PcbLib 二进制文件写入器
/// </summary>
public class PcbLibWriter : IPcbLibWriter
{
    // 简化的文件魔数（实际 Altium 格式需要逆向工程确定）
    private static readonly byte[] MagicNumber = Encoding.ASCII.GetBytes("NPNP_PCBLIB_V1");

    /// <summary>
    /// 将元件写入到新的 PcbLib 文件
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

        // 自动创建目录
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: false);

        WriteHeader(writer, options);
        WriteComponents(writer, components, options);
        WriteFooter(writer);
    }

    /// <summary>
    /// 将元件追加到现有的 PcbLib 文件
    /// </summary>
    /// <param name="outputPath">目标文件路径</param>
    /// <param name="components">要追加的元件集合</param>
    public void Append(string outputPath, IEnumerable<ComponentDetail> components)
    {
        throw new NotImplementedException("追加模式尚未实现。实际 Altium PcbLib 格式需要逆向工程后才能实现追加功能。");
    }

    /// <summary>
    /// 将STEP模型嵌入到指定元件中
    /// </summary>
    /// <param name="outputPath">目标文件路径</param>
    /// <param name="componentName">元件名称</param>
    /// <param name="stepData">STEP模型数据</param>
    public void EmbedStepModel(string outputPath, string componentName, byte[] stepData)
    {
        throw new NotImplementedException("STEP模型嵌入功能尚未实现。实际 Altium PcbLib 格式需要逆向工程后才能实现嵌入功能。");
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
        WriteString(writer, "Npnp.Core PcbLib Writer");
    }

    /// <summary>
    /// 写入所有元件
    /// </summary>
    private void WriteComponents(BinaryWriter writer, IEnumerable<ComponentDetail> components, ExportOptions options)
    {
        var componentList = components.ToList();

        // 写入元件数量
        writer.Write(componentList.Count);

        foreach (var component in componentList)
        {
            WriteFootprint(writer, component, options);
        }
    }

    /// <summary>
    /// 写入单个封装
    /// </summary>
    private void WriteFootprint(BinaryWriter writer, ComponentDetail component, ExportOptions options)
    {
        // 写入元件基本信息
        WriteString(writer, component.LcscId);
        WriteString(writer, component.Name);
        WriteString(writer, component.Description);

        // 写入封装定义
        if (component.Footprint != null)
        {
            writer.Write((byte)1); // 封装存在标志

            var footprint = component.Footprint;
            WriteString(writer, footprint.Name);

            // 写入焊盘
            writer.Write(footprint.Pads.Count);
            foreach (var pad in footprint.Pads)
            {
                WritePad(writer, pad);
            }

            // 写入图形元素
            writer.Write(footprint.Graphics.Count);
            foreach (var graphic in footprint.Graphics)
            {
                WriteGraphic(writer, graphic);
            }

            // 写入封装体尺寸
            if (footprint.Body != null)
            {
                writer.Write((byte)1); // 封装体存在标志
                writer.Write(footprint.Body.Width);
                writer.Write(footprint.Body.Height);
            }
            else
            {
                writer.Write((byte)0); // 封装体不存在标志
            }
        }
        else
        {
            writer.Write((byte)0); // 封装不存在标志
        }

        // 写入STEP模型
        if (options.EmbedStepModel && component.StepModel?.Data != null)
        {
            WriteStepModel(writer, component.StepModel);
        }
        else
        {
            writer.Write((byte)0); // STEP模型不存在标志
        }
    }

    /// <summary>
    /// 写入焊盘
    /// </summary>
    private void WritePad(BinaryWriter writer, Pad pad)
    {
        writer.Write(pad.Number);
        WriteString(writer, pad.Name);
        writer.Write((int)pad.Shape);
        writer.Write(pad.X);
        writer.Write(pad.Y);
        writer.Write(pad.Width);
        writer.Write(pad.Height);
        writer.Write(pad.Rotation);
        writer.Write((int)pad.Layer);
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
    /// 写入STEP模型
    /// </summary>
    private void WriteStepModel(BinaryWriter writer, StepModel stepModel)
    {
        writer.Write((byte)1); // STEP模型存在标志
        WriteString(writer, stepModel.Name);
        WriteString(writer, stepModel.Url);

        if (stepModel.Data != null)
        {
            writer.Write(stepModel.Data.Length);
            writer.Write(stepModel.Data);
        }
        else
        {
            writer.Write(0); // 无数据
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
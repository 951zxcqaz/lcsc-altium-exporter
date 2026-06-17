namespace Npnp.Core.Writers;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using Npnp.Core.Models;
using OpenMcdf;

/// <summary>
/// 真正的 Altium SchLib 格式写入器
/// 使用 CFB (Compound File Binary) 格式，与 npnp 兼容
/// </summary>
public class RealAltiumSchLibWriter : ISchLibWriter
{
    private static readonly Encoding _gbkEncoding;

    static RealAltiumSchLibWriter()
    {
        // 注册 CodePagesEncodingProvider，使 GBK 等编码可用
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        _gbkEncoding = Encoding.GetEncoding("GBK");
    }

    private const double RawPerDxpUnit = 100_000.0;
    private const double GridUnits = 10.0;
    private const double PinLengthUnits = 20.0;
    private const int WhiteBgr = 0xFFFFFF;
    private const int BodyLineWidthIndex = 1;
    private const int GraphicLineWidthIndex = 1;
    private const int BorderBgr = 0x8080F0;
    private const int FillBgr = 0xE0FFFF;
    private const int RedBgr = 0x0000FF;
    private const int BlueBgr = 0xFF0000;

    public void Write(string outputPath, IEnumerable<ComponentDetail> components, ExportOptions options)
    {
        var componentList = components.ToList();
        if (componentList.Count == 0)
        {
            throw new ArgumentException("没有要导出的元件");
        }

        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 删除已存在的文件
        if (File.Exists(outputPath))
        {
            File.Delete(outputPath);
        }

        // 使用 OpenMCDF 创建 CFB 文件
        using var cf = new CompoundFile();

        // 创建文件头
        var fileHeaderStream = new MemoryStream();
        WriteFileHeader(fileHeaderStream, componentList, options.LibraryName);
        AddStreamToRoot(cf, "FileHeader", fileHeaderStream);

        // 创建存储区
        var sectionKey = GenerateSectionKey(options.LibraryName);

        // 创建 SectionKeys
        var sectionKeysStream = new MemoryStream();
        WriteSectionKeys(sectionKeysStream, options.LibraryName, sectionKey);
        AddStreamToRoot(cf, "SectionKeys", sectionKeysStream);

        // 创建组件存储
        var componentStorage = cf.RootStorage.AddStorage(sectionKey);

        // 写入组件数据
        var componentDataStream = new MemoryStream();
        WriteComponentData(componentDataStream, componentList);
        AddStreamToStorage(componentStorage, "Data", componentDataStream);

        // 创建 Storage
        var storageStream = new MemoryStream();
        WriteStorage(storageStream);
        AddStreamToRoot(cf, "Storage", storageStream);

        // 保存文件
        cf.SaveAs(outputPath);
        
        // 调试：确认文件大小
        var fileInfo = new FileInfo(outputPath);
        System.Diagnostics.Debug.WriteLine($"[Writer] SchLib 文件保存成功: {outputPath}, 大小: {fileInfo.Length} bytes");
    }

    private void AddStreamToRoot(CompoundFile cf, string name, MemoryStream data)
    {
        data.Position = 0;
        var bytes = data.ToArray();
        var stream = cf.RootStorage.AddStream(name);
        stream.SetData(bytes);
    }

    private void AddStreamToStorage(CFStorage storage, string name, MemoryStream data)
    {
        data.Position = 0;
        var bytes = data.ToArray();
        var stream = storage.AddStream(name);
        stream.SetData(bytes);
    }

    public void Append(string outputPath, IEnumerable<ComponentDetail> components)
    {
        throw new NotImplementedException("Append not implemented for Altium format");
    }

    private void WriteFileHeader(Stream stream, List<ComponentDetail> components, string libraryName)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 写入 C-string 参数块
        WriteCStringParam(writer, "HEADER", "Protel for Windows - Schematic Library Editor Binary File Version 5.0");
        WriteCStringParam(writer, "WEIGHT", components.Count.ToString());
        WriteCStringParam(writer, "MINORVERSION", "2");
        WriteCStringParam(writer, "FONTIDCOUNT", "1");
        WriteCStringParam(writer, "SIZE1", "10");
        WriteCStringParam(writer, "FONTNAME1", "Times New Roman");
        WriteCStringParam(writer, "USEMBCS", "T");
        WriteCStringParam(writer, "ISBOC", "T");
        WriteCStringParam(writer, "SHEETSTYLE", "9");
        WriteCStringParam(writer, "SYSTEMFONT", "1");
        WriteCStringParam(writer, "BORDERON", "T");
        WriteCStringParam(writer, "SHEETNUMBERSPACESIZE", "12");
        WriteCStringParam(writer, "AREACOLOR", "16317695");
        WriteCStringParam(writer, "SNAPGRIDON", "T");
        WriteCStringParam(writer, "SNAPGRIDSIZE", "10");
        WriteCStringParam(writer, "VISIBLEGRIDON", "T");
        WriteCStringParam(writer, "VISIBLEGRIDSIZE", "10");
        WriteCStringParam(writer, "CUSTOMX", "18000");
        WriteCStringParam(writer, "CUSTOMY", "18000");
        WriteCStringParam(writer, "USECUSTOMSHEET", "T");
        WriteCStringParam(writer, "REFERENCEZONESON", "T");
        WriteCStringParam(writer, "DISPLAY_UNIT", "0");
        WriteCStringParam(writer, "COMPCOUNT", components.Count.ToString());

        for (int i = 0; i < components.Count; i++)
        {
            WriteCStringParam(writer, $"LIBREF{i}", components[i].Name);
            WriteCStringParam(writer, $"COMPDESCR{i}", components[i].Description ?? "Generated from EasyEDA");
            WriteCStringParam(writer, $"PARTCOUNT{i}", "2");
        }

        writer.Write((byte)0); // 结束标记
        writer.Write(BinaryPrimitives.ReverseEndianness(1)); // i32 = 1
        WriteStringBlock(writer, libraryName);
    }

    private void WriteSectionKeys(Stream stream, string libraryName, string sectionKey)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        WriteCStringParam(writer, "KeyCount", "1");
        WriteCStringParam(writer, "LibRef0", libraryName);
        WriteCStringParam(writer, "SectionKey0", sectionKey);
    }

    private void WriteComponentData(Stream stream, List<ComponentDetail> components)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        writer.Write(components.Count);

        foreach (var component in components)
        {
            WriteComponent(writer, component);
        }
    }

    private void WriteComponent(BinaryWriter writer, ComponentDetail component)
    {
        // 解析符号数据
        var symbolData = ParseSymbolData(component.SymbolJson);

        // 写入符号名称
        WriteGbkString(writer, component.Name);

        // 写入描述
        WriteGbkString(writer, component.Description ?? "");

        // 写入设计器
        WriteGbkString(writer, "*");

        // 写入注释
        WriteGbkString(writer, "*");

        // 写入参数数量
        writer.Write(0);

        // 写入引脚数量
        var pinCount = symbolData.Pins.Count;
        writer.Write(pinCount);

        // 写入每个引脚
        foreach (var pin in symbolData.Pins)
        {
            WritePin(writer, pin);
        }

        // 写入图形元素
        WriteGraphics(writer, symbolData);

        // 写入实现
        WriteImplementations(writer, component);
    }

    private void WritePin(BinaryWriter writer, ParsedPin pin)
    {
        // 引脚设计器
        WriteGbkString(writer, pin.Designator);

        // 引脚名称
        WriteGbkString(writer, pin.Name);

        // 位置
        writer.Write(pin.X);
        writer.Write(pin.Y);

        // 长度
        writer.Write(pin.Length);

        // 旋转角度
        writer.Write((float)pin.Rotation);

        // 方向
        writer.Write((byte)pin.Orientation);

        // 显示名称
        writer.Write((byte)(pin.ShowName ? 1 : 0));

        // 显示设计器
        writer.Write((byte)1);

        // 颜色
        writer.Write(RedBgr);

        // 线宽索引
        writer.Write((byte)0);

        // 引脚类型
        writer.Write((byte)0);

        // 空白
        writer.Write((int)0);
    }

    private void WriteGraphics(BinaryWriter writer, ParsedSymbolData symbolData)
    {
        // 线段
        writer.Write(symbolData.Lines.Count);
        foreach (var line in symbolData.Lines)
        {
            WriteLine(writer, line);
        }

        // 矩形
        writer.Write(symbolData.Rectangles.Count);
        foreach (var rect in symbolData.Rectangles)
        {
            WriteRectangle(writer, rect);
        }

        // 圆弧
        writer.Write(symbolData.Arcs.Count);
        foreach (var arc in symbolData.Arcs)
        {
            WriteArc(writer, arc);
        }

        // 椭圆
        writer.Write(0);

        // 多边形
        writer.Write(0);

        // 文本
        writer.Write(symbolData.Texts.Count);
        foreach (var text in symbolData.Texts)
        {
            WriteText(writer, text);
        }
    }

    private void WriteLine(BinaryWriter writer, ParsedLine line)
    {
        writer.Write(line.X1);
        writer.Write(line.Y1);
        writer.Write(line.X2);
        writer.Write(line.Y2);
        writer.Write(line.Width);
        writer.Write(0); // Color
        writer.Write((byte)0); // LineStyle
        writer.Write((byte)0); // EndCapStyle
    }

    private void WriteRectangle(BinaryWriter writer, ParsedRectangle rect)
    {
        writer.Write(rect.X1);
        writer.Write(rect.Y1);
        writer.Write(rect.X2);
        writer.Write(rect.Y2);
        writer.Write(rect.LineWidth);
        writer.Write(rect.Color);
        writer.Write(rect.FillColor);
        writer.Write(rect.IsFilled ? (byte)1 : (byte)0);
        writer.Write(rect.IsTransparent ? (byte)1 : (byte)0);
    }

    private void WriteArc(BinaryWriter writer, ParsedArc arc)
    {
        writer.Write(arc.X);
        writer.Write(arc.Y);
        writer.Write(arc.Radius);
        writer.Write((float)arc.StartAngle);
        writer.Write((float)arc.EndAngle);
        writer.Write(arc.LineWidth);
        writer.Write(0); // Color
    }

    private void WriteText(BinaryWriter writer, ParsedText text)
    {
        WriteGbkString(writer, text.Content);
        writer.Write(text.X);
        writer.Write(text.Y);
        writer.Write((float)text.Rotation);
        writer.Write(text.Height);
        writer.Write(text.Color);
        writer.Write((byte)0); // FontId
        writer.Write((byte)0); // Justification
        writer.Write((byte)0); // Mirror
    }

    private void WriteImplementations(BinaryWriter writer, ComponentDetail component)
    {
        // 实现数量
        writer.Write(1);

        // 默认实现
        WriteGbkString(writer, component.Package ?? "");
        WriteGbkString(writer, "");
        WriteGbkString(writer, "PCLib");
        writer.Write((int)0); // Flags

        // 引脚映射数量
        writer.Write(0);
    }

    private void WriteStorage(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        WriteCStringParam(writer, "LibraryName", "");
    }

    private void WriteCStringParam(BinaryWriter writer, string key, string value)
    {
        var keyBytes = Encoding.UTF8.GetBytes(key);
        writer.Write((byte)keyBytes.Length);
        writer.Write(keyBytes);

        var valueBytes = Encoding.UTF8.GetBytes(value);
        writer.Write(BinaryPrimitives.ReverseEndianness((short)valueBytes.Length));
        writer.Write(valueBytes);
    }

    private void WriteStringBlock(BinaryWriter writer, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        writer.Write(BinaryPrimitives.ReverseEndianness((short)bytes.Length));
        writer.Write(bytes);
    }

    private void WriteGbkString(BinaryWriter writer, string text)
    {
        var bytes = _gbkEncoding.GetBytes(text ?? "");
        writer.Write(BinaryPrimitives.ReverseEndianness((short)bytes.Length));
        writer.Write(bytes);
    }

    private string GenerateSectionKey(string name)
    {
        return name.Replace(" ", "_");
    }

    private ParsedSymbolData ParseSymbolData(string? json)
    {
        var result = new ParsedSymbolData();

        if (string.IsNullOrEmpty(json))
        {
            System.Diagnostics.Debug.WriteLine("[Writer] ParseSymbolData: json is null or empty");
            return result;
        }

        System.Diagnostics.Debug.WriteLine($"[Writer] ParseSymbolData: json length={json.Length}, startsWith={json.Substring(0, Math.Min(50, json.Length))}");

        // 直接检查是否是 dataStr 格式（包含换行分隔的 JSON 行）
        // dataStr 格式: 每行是一个 JSON 数组，如 ["DOCTYPE","SYMBOL","1.1"] 或 ["PIN","1","U1",...]
        // 格式可能是：
        //   - ["DOCTYPE","SYMBOL","1.1"] 开头（嘉立创 EDA 符号格式）
        //   - ["PIN","1",...] 开头
        //   - ["LINE",...] 开头
        //   - ["RECT",...] 开头
        if (json.Contains('\n') && json.TrimStart().StartsWith("["))
        {
            System.Diagnostics.Debug.WriteLine("[Writer] ParseSymbolData: using direct dataStr parsing");
            // 这是直接的 dataStr 格式，直接解析
            var lines = json.Split('\n');
            int pinCount = 0, lineCount = 0, rectCount = 0, arcCount = 0, textCount = 0;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                try
                {
                    var row = JsonSerializer.Deserialize<List<JsonElement>>(trimmed);
                    if (row == null || row.Count == 0) continue;

                    var type = row[0].GetString()?.ToUpperInvariant() ?? "";
                    switch (type)
                    {
                        case "PIN":
                            ParsePin(row, result);
                            pinCount++;
                            break;
                        case "LINE":
                            ParseLine(row, result);
                            lineCount++;
                            break;
                        case "LINESTYLE":
                            // 忽略线型定义
                            break;
                        case "FONTSTYLE":
                            // 忽略字体样式定义
                            break;
                        case "RECT":
                            ParseRect(row, result);
                            rectCount++;
                            break;
                        case "ARC":
                            ParseArc(row, result);
                            arcCount++;
                            break;
                        case "TEXT":
                            ParseText(row, result);
                            textCount++;
                            break;
                        case "DOCTYPE":
                        case "HEAD":
                        case "PART":
                        case "ATTR":
                            // 忽略文档类型、头部、部件、属性定义
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Writer] Parse error for line: {trimmed.Substring(0, Math.Min(50, trimmed.Length))}, error: {ex.Message}");
                    // 忽略解析错误
                }
            }
            System.Diagnostics.Debug.WriteLine($"[Writer] ParseSymbolData: pins={pinCount}, lines={lineCount}, rects={rectCount}, arcs={arcCount}, texts={textCount}");
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 查找数据字符串
            string? dataStr = null;
            if (root.TryGetProperty("result", out var resultProp) &&
                resultProp.TryGetProperty("dataStr", out var dataStrElement))
            {
                dataStr = dataStrElement.GetString();
            }
            else if (root.TryGetProperty("dataStr", out var dataStrElement2))
            {
                dataStr = dataStrElement2.GetString();
            }
            else if (root.TryGetProperty("data", out var dataProp))
            {
                dataStr = dataProp.GetString();
            }
            else if (root.ValueKind == JsonValueKind.String)
            {
                dataStr = root.GetString();
            }

            if (string.IsNullOrEmpty(dataStr))
            {
                return result;
            }

            // 解析数据行
            var lines = dataStr.Split('\n');
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                try
                {
                    var row = JsonSerializer.Deserialize<List<JsonElement>>(trimmed);
                    if (row == null || row.Count == 0) continue;

                    var type = row[0].GetString()?.ToUpperInvariant() ?? "";
                    switch (type)
                    {
                        case "PIN":
                            ParsePin(row, result);
                            break;
                        case "LINE":
                            ParseLine(row, result);
                            break;
                        case "RECT":
                            ParseRect(row, result);
                            break;
                        case "ARC":
                            ParseArc(row, result);
                            break;
                        case "TEXT":
                            ParseText(row, result);
                            break;
                    }
                }
                catch
                {
                    // 忽略解析错误
                }
            }
        }
        catch
        {
            // 忽略解析错误
        }

        return result;
    }

    private void ParsePin(List<JsonElement> row, ParsedSymbolData result)
    {
        if (row.Count < 8) return;

        var pin = new ParsedPin
        {
            Designator = row.Count > 1 && row[1].ValueKind == JsonValueKind.String ? row[1].GetString() ?? "" : "",
            Name = "",
            X = ToDxpUnits(row.Count > 4 ? row[4].GetDouble() : 0),
            Y = ToDxpUnits(row.Count > 5 ? row[5].GetDouble() : 0),
            Length = ToDxpUnits(row.Count > 6 ? row[6].GetDouble() : 20),
            Rotation = row.Count > 7 ? row[7].GetDouble() : 0
        };

        // 确定引脚方向
        var angle = NormalizeAngle(pin.Rotation);
        if (angle >= 45 && angle < 135)
        {
            pin.Orientation = 1; // Right
        }
        else if (angle >= 135 && angle < 225)
        {
            pin.Orientation = 2; // Down
        }
        else if (angle >= 225 && angle < 315)
        {
            pin.Orientation = 3; // Left
        }
        else
        {
            pin.Orientation = 0; // Up
        }

        result.Pins.Add(pin);
    }

    private void ParseLine(List<JsonElement> row, ParsedSymbolData result)
    {
        if (row.Count < 5) return;

        result.Lines.Add(new ParsedLine
        {
            X1 = ToDxpUnits(row[1].GetDouble()),
            Y1 = ToDxpUnits(row[2].GetDouble()),
            X2 = ToDxpUnits(row[3].GetDouble()),
            Y2 = ToDxpUnits(row[4].GetDouble()),
            Width = ToDxpUnits(row.Count > 5 ? row[5].GetDouble() : 0.1)
        });
    }

    private void ParseRect(List<JsonElement> row, ParsedSymbolData result)
    {
        if (row.Count < 5) return;

        var x1 = row[1].GetDouble();
        var y1 = row[2].GetDouble();
        var x2 = row[3].GetDouble();
        var y2 = row[4].GetDouble();

        result.Rectangles.Add(new ParsedRectangle
        {
            X1 = ToDxpUnits(x1),
            Y1 = ToDxpUnits(y1),
            X2 = ToDxpUnits(x2),
            Y2 = ToDxpUnits(y2),
            LineWidth = ToDxpUnits(0.1),
            Color = BorderBgr,
            FillColor = FillBgr,
            IsFilled = true,
            IsTransparent = false
        });
    }

    private void ParseArc(List<JsonElement> row, ParsedSymbolData result)
    {
        if (row.Count < 6) return;

        result.Arcs.Add(new ParsedArc
        {
            X = ToDxpUnits(row[1].GetDouble()),
            Y = ToDxpUnits(row[2].GetDouble()),
            Radius = ToDxpUnits(row[3].GetDouble()),
            StartAngle = row[4].GetDouble(),
            EndAngle = row[5].GetDouble(),
            LineWidth = ToDxpUnits(0.1)
        });
    }

    private void ParseText(List<JsonElement> row, ParsedSymbolData result)
    {
        if (row.Count < 4) return;

        result.Texts.Add(new ParsedText
        {
            Content = row[3].GetString() ?? "",
            X = ToDxpUnits(row[1].GetDouble()),
            Y = ToDxpUnits(row[2].GetDouble()),
            Rotation = row.Count > 4 ? row[4].GetDouble() : 0,
            Height = ToDxpUnits(60),
            Color = 0
        });
    }

    private int ToDxpUnits(double value)
    {
        return (int)(value * RawPerDxpUnit);
    }

    private double NormalizeAngle(double angle)
    {
        angle = angle % 360;
        if (angle < 0) angle += 360;
        return angle;
    }

    private class ParsedSymbolData
    {
        public List<ParsedPin> Pins { get; } = new();
        public List<ParsedLine> Lines { get; } = new();
        public List<ParsedRectangle> Rectangles { get; } = new();
        public List<ParsedArc> Arcs { get; } = new();
        public List<ParsedText> Texts { get; } = new();
    }

    private class ParsedPin
    {
        public string Designator { get; set; } = "";
        public string Name { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public int Length { get; set; }
        public double Rotation { get; set; }
        public int Orientation { get; set; }
        public bool ShowName { get; set; } = true;
    }

    private class ParsedLine
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int Width { get; set; }
    }

    private class ParsedRectangle
    {
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int LineWidth { get; set; }
        public int Color { get; set; }
        public int FillColor { get; set; }
        public bool IsFilled { get; set; }
        public bool IsTransparent { get; set; }
    }

    private class ParsedArc
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public int LineWidth { get; set; }
    }

    private class ParsedText
    {
        public string Content { get; set; } = "";
        public int X { get; set; }
        public int Y { get; set; }
        public double Rotation { get; set; }
        public int Height { get; set; }
        public int Color { get; set; }
    }
}

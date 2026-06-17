namespace Npnp.Core.Writers;

using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using Npnp.Core.Models;
using OpenMcdf;

/// <summary>
/// 真正的 Altium PcbLib 格式写入器
/// 使用 CFB (Compound File Binary) 格式，与 npnp 兼容
/// </summary>
public class RealAltiumPcbLibWriter : IPcbLibWriter
{
    // 层定义
    private const byte LayerTop = 1;
    private const byte LayerBottom = 32;
    private const byte LayerMulti = 74;

    // 焊盘形状
    private const byte PadShapeRound = 1;
    private const byte PadShapeRectangular = 2;
    private const byte PadShapeOctagonal = 3;
    private const byte PadShapeRoundedRectangle = 9;

    // 焊孔类型
    private const byte PadHoleRound = 0;
    private const byte PadHoleSlot = 2;

    private const int FlagBase = 0x08;
    private const int FlagUnlocked = 0x04;

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

        // 写入文件头
        var fileHeaderStream = new MemoryStream();
        WriteFileHeader(fileHeaderStream);
        AddStreamToRoot(cf, "FileHeader", fileHeaderStream);

        // 收集所有封装的 section keys
        var sections = componentList.Select((c, i) => (Component: c, Key: GenerateSectionKey(c.Package ?? c.Name, i))).ToList();

        // 写入 SectionKeys
        if (sections.Any(s => s.Key != s.Component.Name))
        {
            var sectionKeysStream = new MemoryStream();
            WriteSectionKeys(sectionKeysStream, sections);
            AddStreamToRoot(cf, "SectionKeys", sectionKeysStream);
        }

        // 创建 Library 存储
        var libraryStorage = cf.RootStorage.AddStorage("Library");

        // Library/Header
        var libraryHeaderStream = new MemoryStream();
        WriteStorageHeader(libraryHeaderStream, componentList.Count);
        AddStreamToStorage(libraryStorage, "Header", libraryHeaderStream);

        // Library/Data
        var libraryDataStream = new MemoryStream();
        WriteLibraryData(libraryDataStream, componentList, outputPath);
        AddStreamToStorage(libraryStorage, "Data", libraryDataStream);

        // Library/Models/
        var modelsStorage = libraryStorage.AddStorage("Models");
        var modelsHeaderStream = new MemoryStream();
        WriteStorageHeader(modelsHeaderStream, 0); // 暂无嵌入模型
        AddStreamToStorage(modelsStorage, "Header", modelsHeaderStream);
        AddStreamToStorage(modelsStorage, "Data", new MemoryStream());

        // Library/Textures/
        var texturesStorage = libraryStorage.AddStorage("Textures");
        var texturesHeaderStream = new MemoryStream();
        WriteStorageHeader(texturesHeaderStream, 0);
        AddStreamToStorage(texturesStorage, "Header", texturesHeaderStream);
        AddStreamToStorage(texturesStorage, "Data", new MemoryStream());

        // Library/ModelsNoEmbed/
        var modelsNoEmbedStorage = libraryStorage.AddStorage("ModelsNoEmbed");
        var modelsNoEmbedHeaderStream = new MemoryStream();
        WriteStorageHeader(modelsNoEmbedHeaderStream, 0);
        AddStreamToStorage(modelsNoEmbedStorage, "Header", modelsNoEmbedHeaderStream);
        AddStreamToStorage(modelsNoEmbedStorage, "Data", new MemoryStream());

        // 为每个封装创建存储
        foreach (var (component, sectionKey) in sections)
        {
            var componentStorage = cf.RootStorage.AddStorage(sectionKey);

            // 解析封装数据
            var footprintData = ParseFootprintData(component.FootprintJson);

            // Header
            var headerStream = new MemoryStream();
            WriteStorageHeader(headerStream, footprintData.TotalPrimitiveCount);
            AddStreamToStorage(componentStorage, "Header", headerStream);

            // Parameters
            var paramsStream = new MemoryStream();
            WriteComponentParameters(paramsStream, component);
            AddStreamToStorage(componentStorage, "Parameters", paramsStream);

            // WideStrings
            AddStreamToStorage(componentStorage, "WideStrings", new MemoryStream());

            // Data
            var dataStream = new MemoryStream();
            WriteComponentData(dataStream, footprintData);
            AddStreamToStorage(componentStorage, "Data", dataStream);

            // UniqueIdPrimitiveInformation
            var uniqueIdStorage = componentStorage.AddStorage("UniqueIdPrimitiveInformation");
            var uniqueIdHeaderStream = new MemoryStream();
            WriteStorageHeader(uniqueIdHeaderStream, footprintData.TotalPrimitiveCount);
            AddStreamToStorage(uniqueIdStorage, "Header", uniqueIdHeaderStream);
            AddStreamToStorage(uniqueIdStorage, "Data", new MemoryStream());
        }

        // 保存文件
        cf.SaveAs(outputPath);
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

    public void EmbedStepModel(string outputPath, string componentName, byte[] stepData)
    {
        throw new NotImplementedException("STEP model embedding not implemented");
    }

    private void WriteFileHeader(Stream stream)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // PCB 6.0 Binary Library File
        var headerText = "PCB 6.0 Binary Library File";
        writer.Write(headerText.Length);
        writer.Write(Encoding.UTF8.GetBytes(headerText));
    }

    private void WriteSectionKeys(Stream stream, List<(ComponentDetail Component, string Key)> sections)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        var filtered = sections.Where(s => s.Key != s.Component.Name).ToList();
        writer.Write(filtered.Count);

        foreach (var (component, key) in filtered)
        {
            WritePascalString(writer, component.Name);
            WriteStringBlock(writer, key);
        }
    }

    private void WriteStorageHeader(Stream stream, int recordCount)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write(recordCount);
    }

    private void WriteLibraryData(Stream stream, List<ComponentDetail> components, string outputPath)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 写入参数块
        var filename = Path.GetFileName(outputPath);
        var now = DateTime.Now;
        var dateStr = $"{now.Month}/{now.Day}/{now.Year}";
        var timeStr = $"{now.Hour}:{now.Minute:D2}:{now.Second:D2} {(now.Hour >= 12 ? "PM" : "AM")}";

        WriteCStringBlock(writer, new Dictionary<string, string>
        {
            { "SourceFile", filename },
            { "Date", dateStr },
            { "Time", timeStr }
        });

        // 写入元件数量和名称
        writer.Write(components.Count);
        foreach (var component in components)
        {
            WriteStringBlock(writer, component.Package ?? component.Name);
        }
    }

    private void WriteComponentParameters(Stream stream, ComponentDetail component)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        var params_ = new Dictionary<string, string>
        {
            { "PATTERN", component.Package ?? component.Name },
            { "HEIGHT", "0" }
        };

        if (!string.IsNullOrEmpty(component.Description))
        {
            params_["DESCRIPTION"] = component.Description;
        }

        WriteCStringParamBlock(writer, params_);
    }

    private void WriteComponentData(Stream stream, ParsedFootprintData data)
    {
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);

        // 写入焊盘
        writer.Write(data.Pads.Count);
        foreach (var pad in data.Pads)
        {
            WritePad(writer, pad);
        }

        // 写入线段
        writer.Write(data.Tracks.Count);
        foreach (var track in data.Tracks)
        {
            WriteTrack(writer, track);
        }

        // 写入圆弧
        writer.Write(data.Arcs.Count);
        foreach (var arc in data.Arcs)
        {
            WriteArc(writer, arc);
        }

        // 写入区域 (0)
        writer.Write(0);

        // 写入元件体 (0)
        writer.Write(0);
    }

    private void WritePad(BinaryWriter writer, ParsedPad pad)
    {
        // 设计器
        WritePascalString(writer, pad.Designator);

        // 位置
        writer.Write(pad.X);
        writer.Write(pad.Y);

        // 尺寸
        writer.Write(pad.SizeX);
        writer.Write(pad.SizeY);
        writer.Write(pad.SizeX); // middle
        writer.Write(pad.SizeY); // middle
        writer.Write(pad.SizeX); // bottom
        writer.Write(pad.SizeY); // bottom

        // 形状
        writer.Write(pad.ShapeTop);
        writer.Write(pad.ShapeMiddle);
        writer.Write(pad.ShapeBottom);

        // 旋转
        writer.Write((float)pad.Rotation);

        // 镀孔
        writer.Write(pad.IsPlated ? (byte)1 : (byte)0);

        // 层
        writer.Write(pad.Layer);

        // 孔径
        writer.Write(pad.HoleSize);

        // 孔类型
        writer.Write(pad.HoleType);

        // 属性
        var flags = FlagBase | FlagUnlocked;
        if (pad.IsTentingTop) flags |= 0x20;
        if (pad.IsTentingBottom) flags |= 0x40;
        writer.Write((short)flags);

        // 其他参数
        writer.Write((byte)0); // mode
        writer.Write((byte)0); // powerPlaneConnectStyle
        writer.Write((int)0); // reliefAirGap
        writer.Write((int)0); // reliefConductorWidth
        writer.Write((short)0); // reliefEntries
        writer.Write((int)0); // powerPlaneClearance
        writer.Write((int)0); // powerPlaneReliefExpansion
        writer.Write((int)0); // pasteMaskExpansion
        writer.Write((int)0); // solderMaskExpansion

        // 角半径百分比
        writer.Write((byte)0);
    }

    private void WriteTrack(BinaryWriter writer, ParsedTrack track)
    {
        // 层
        writer.Write(track.Layer);

        // 起点
        writer.Write(track.X1);
        writer.Write(track.Y1);

        // 终点
        writer.Write(track.X2);
        writer.Write(track.Y2);

        // 宽度
        writer.Write(track.Width);

        // 属性
        var flags = FlagBase | FlagUnlocked;
        if (track.IsTentingTop) flags |= 0x20;
        if (track.IsTentingBottom) flags |= 0x40;
        writer.Write((short)flags);

        // 网络索引
        writer.Write((ushort)0);

        // 元件索引
        writer.Write((byte)0);
    }

    private void WriteArc(BinaryWriter writer, ParsedArc arc)
    {
        // 层
        writer.Write(arc.Layer);

        // 中心
        writer.Write(arc.X);
        writer.Write(arc.Y);

        // 半径
        writer.Write(arc.Radius);

        // 角度
        writer.Write((float)arc.StartAngle);
        writer.Write((float)arc.EndAngle);

        // 宽度
        writer.Write(arc.Width);

        // 属性
        var flags = FlagBase | FlagUnlocked;
        if (arc.IsTentingTop) flags |= 0x20;
        if (arc.IsTentingBottom) flags |= 0x40;
        writer.Write((short)flags);
    }

    private void WriteCStringBlock(BinaryWriter writer, Dictionary<string, string> parameters)
    {
        WriteCStringParamBlock(writer, parameters);
    }

    private void WriteCStringParamBlock(BinaryWriter writer, Dictionary<string, string> parameters)
    {
        foreach (var (key, value) in parameters)
        {
            var keyBytes = Encoding.UTF8.GetBytes(key);
            writer.Write((byte)keyBytes.Length);
            writer.Write(keyBytes);

            var valueBytes = Encoding.UTF8.GetBytes(value);
            var len = valueBytes.Length;
            writer.Write(BinaryPrimitives.ReverseEndianness((short)len));
            writer.Write(valueBytes);
        }

        writer.Write((byte)0); // 结束标记
    }

    private void WritePascalString(BinaryWriter writer, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        writer.Write((byte)bytes.Length);
        writer.Write(bytes);
    }

    private void WriteStringBlock(BinaryWriter writer, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text ?? "");
        writer.Write(BinaryPrimitives.ReverseEndianness((short)bytes.Length));
        writer.Write(bytes);
    }

    private string GenerateSectionKey(string name, int index)
    {
        var key = new StringBuilder();
        foreach (var c in name)
        {
            if (key.Length >= 31) break;
            if (char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-' || c == '.')
            {
                key.Append(c);
            }
            else
            {
                key.Append('_');
            }
        }

        if (key.Length == 0)
        {
            return index.ToString();
        }

        return key.ToString();
    }

    private ParsedFootprintData ParseFootprintData(string? json)
    {
        var result = new ParsedFootprintData();

        if (string.IsNullOrEmpty(json))
        {
            return result;
        }

        // 直接检查是否是 dataStr 格式（包含换行分隔的 JSON 行）
        // dataStr 格式: 每行是一个 JSON 数组，如 ["DOCTYPE","FOOTPRINT","1.8"] 或 ["PAD","1",...]
        // 格式可能是：
        //   - ["DOCTYPE","FOOTPRINT","1.8"] 开头（嘉立创 EDA 封装格式）
        //   - ["PAD","1",...] 开头
        //   - ["LINE",...] 或 ["L",...] 开头
        //   - ["ARC",...] 或 ["A",...] 开头
        //   - ["RECT",...] 或 ["R",...] 开头
        if (json.Contains('\n') && json.TrimStart().StartsWith("["))
        {
            // 这是直接的 dataStr 格式，直接解析
            var lines = json.Split('\n');
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
                        case "PAD":
                            ParsePad(row, result);
                            break;
                        case "LINE":
                        case "L":
                            ParseLine(row, result);
                            break;
                        case "ARC":
                        case "A":
                            ParseArc(row, result);
                            break;
                        case "RECT":
                        case "R":
                            ParseRect(row, result);
                            break;
                        case "LAYER":
                            // 忽略层定义
                            break;
                        case "DOCTYPE":
                        case "HEAD":
                            // 忽略文档类型、头部定义
                            break;
                    }
                }
                catch
                {
                    // 忽略解析错误
                }
            }
            return result;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // 查找数据
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
                        case "PAD":
                            ParsePad(row, result);
                            break;
                        case "LINE":
                        case "L":
                            ParseLine(row, result);
                            break;
                        case "ARC":
                        case "A":
                            ParseArc(row, result);
                            break;
                        case "RECT":
                        case "R":
                            ParseRect(row, result);
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

    private void ParsePad(List<JsonElement> row, ParsedFootprintData result)
    {
        if (row.Count < 8) return;

        var pad = new ParsedPad
        {
            Designator = row.Count > 1 ? (row[1].GetString() ?? "1") : "1",
            X = ToDxpUnits(row.Count > 3 ? row[3].GetDouble() : 0),
            Y = ToDxpUnits(row.Count > 4 ? row[4].GetDouble() : 0),
            SizeX = ToDxpUnits(row.Count > 5 ? row[5].GetDouble() : 1.5),
            SizeY = ToDxpUnits(row.Count > 6 ? row[6].GetDouble() : 1.5),
            HoleSize = ToDxpUnits(row.Count > 7 ? row[7].GetDouble() : 0),
            ShapeTop = PadShapeRoundedRectangle,
            ShapeMiddle = PadShapeRoundedRectangle,
            ShapeBottom = PadShapeRoundedRectangle,
            Layer = LayerMulti,
            IsPlated = true,
            IsTentingTop = true,
            IsTentingBottom = true,
            HoleType = PadHoleRound
        };

        result.Pads.Add(pad);
    }

    private void ParseLine(List<JsonElement> row, ParsedFootprintData result)
    {
        if (row.Count < 6) return;

        var track = new ParsedTrack
        {
            X1 = ToDxpUnits(row[1].GetDouble()),
            Y1 = ToDxpUnits(row[2].GetDouble()),
            X2 = ToDxpUnits(row[3].GetDouble()),
            Y2 = ToDxpUnits(row[4].GetDouble()),
            Width = ToDxpUnits(row.Count > 5 ? row[5].GetDouble() : 0.2),
            Layer = LayerTop // 丝印层
        };

        result.Tracks.Add(track);
    }

    private void ParseRect(List<JsonElement> row, ParsedFootprintData result)
    {
        if (row.Count < 5) return;

        var x1 = row[1].GetDouble();
        var y1 = row[2].GetDouble();
        var x2 = row[3].GetDouble();
        var y2 = row[4].GetDouble();

        var track1 = new ParsedTrack { X1 = ToDxpUnits(x1), Y1 = ToDxpUnits(y1), X2 = ToDxpUnits(x2), Y2 = ToDxpUnits(y1), Layer = LayerTop };
        var track2 = new ParsedTrack { X1 = ToDxpUnits(x2), Y1 = ToDxpUnits(y1), X2 = ToDxpUnits(x2), Y2 = ToDxpUnits(y2), Layer = LayerTop };
        var track3 = new ParsedTrack { X1 = ToDxpUnits(x2), Y1 = ToDxpUnits(y2), X2 = ToDxpUnits(x1), Y2 = ToDxpUnits(y2), Layer = LayerTop };
        var track4 = new ParsedTrack { X1 = ToDxpUnits(x1), Y1 = ToDxpUnits(y2), X2 = ToDxpUnits(x1), Y2 = ToDxpUnits(y1), Layer = LayerTop };

        foreach (var track in new[] { track1, track2, track3, track4 })
        {
            track.Width = ToDxpUnits(0.2);
            result.Tracks.Add(track);
        }
    }

    private void ParseArc(List<JsonElement> row, ParsedFootprintData result)
    {
        if (row.Count < 6) return;

        result.Arcs.Add(new ParsedArc
        {
            X = ToDxpUnits(row[1].GetDouble()),
            Y = ToDxpUnits(row[2].GetDouble()),
            Radius = ToDxpUnits(row[3].GetDouble()),
            StartAngle = row[4].GetDouble(),
            EndAngle = row[5].GetDouble(),
            Width = ToDxpUnits(0.2),
            Layer = LayerTop
        });
    }

    private int ToDxpUnits(double value)
    {
        return (int)(value * 10000); // 1 mil = 1 units
    }

    private class ParsedFootprintData
    {
        public List<ParsedPad> Pads { get; } = new();
        public List<ParsedTrack> Tracks { get; } = new();
        public List<ParsedArc> Arcs { get; } = new();

        public int TotalPrimitiveCount => Pads.Count + Tracks.Count + Arcs.Count;
    }

    private class ParsedPad
    {
        public string Designator { get; set; } = "1";
        public int X { get; set; }
        public int Y { get; set; }
        public int SizeX { get; set; }
        public int SizeY { get; set; }
        public int HoleSize { get; set; }
        public byte ShapeTop { get; set; }
        public byte ShapeMiddle { get; set; }
        public byte ShapeBottom { get; set; }
        public double Rotation { get; set; }
        public bool IsPlated { get; set; }
        public byte Layer { get; set; }
        public bool IsTentingTop { get; set; }
        public bool IsTentingBottom { get; set; }
        public byte HoleType { get; set; }
    }

    private class ParsedTrack
    {
        public byte Layer { get; set; }
        public int X1 { get; set; }
        public int Y1 { get; set; }
        public int X2 { get; set; }
        public int Y2 { get; set; }
        public int Width { get; set; }
        public bool IsTentingTop { get; set; }
        public bool IsTentingBottom { get; set; }
    }

    private class ParsedArc
    {
        public byte Layer { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Radius { get; set; }
        public double StartAngle { get; set; }
        public double EndAngle { get; set; }
        public int Width { get; set; }
        public bool IsTentingTop { get; set; }
        public bool IsTentingBottom { get; set; }
    }
}

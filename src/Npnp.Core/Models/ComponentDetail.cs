using System.Collections.Generic;

namespace Npnp.Core.Models;

/// <summary>
/// 元件详情 - 包含 LCSC 元件的所有信息
/// </summary>
public class ComponentDetail
{
    // = = = = = 基础字段 = = = = =
    public string LcscId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Manufacturer { get; set; }
    public string Package { get; set; }

    // = = = = = LCSC / 型号信息 = = = = =
    public string? LcscPartName { get; set; }        // LCSC 元件规格名（如 1MΩ ±1% 250mW）
    public string? ManufacturerPart { get; set; }     // 厂家型号（如 1206W4F1004T5E）
    public string? Supplier { get; set; }              // 供应商（通常 LCSC）
    public string? PartClass { get; set; }             // 元件类别（如 Basic Part）
    public string? DatasheetUrl { get; set; }          // Datasheet 链接
    public string? ImageUrl { get; set; }             // 产品图片 URL
    public string? Designator { get; set; }           // 位号（如 R?, C?, U?）
    public string? Uuid { get; set; }                 // 整体 UUID

    // = = = = = 电气参数 = = = = =
    public string? Value { get; set; }                 // 数值（如 1MΩ、100nF）
    public string? Tolerance { get; set; }             // 精度（如 ±1%、±20%）
    public string? Power { get; set; }                 // 功率（如 250mW）
    public string? Voltage { get; set; }               // 电压（如 50V）
    public string? TemperatureCoefficient { get; set; } // 温度系数
    public string? OperatingTemperature { get; set; }  // 工作温度范围
    public string? ComponentType { get; set; }         // 元件类型（如 厚膜电阻、贴片电容）
    public string? Category { get; set; }              // 类别（如 电阻）
    public string? SubCategory { get; set; }           // 子类别（如 贴片电阻）
    public string? SpecSummary { get; set; }           // 规格摘要（综合文本）

    // = = = = = UUID 字段（用于下载 symbol/footprint/3D） = = = = =
    public string? SymbolUuid { get; set; }
    public string? FootprintUuid { get; set; }
    public string? Model3DUuid { get; set; }

    // = = = = = JSON 源数据（EasyEDA 原始符号/封装描述） = = = = =
    public string? SymbolJson { get; set; }
    public string? FootprintJson { get; set; }

    // = = = = = 动态属性 = = = = =
    public Dictionary<string, string> AllAttributes { get; set; } = new();

    // = = = = = 解析后的结构化对象（保留用于更高级的导出） = = = = =
    public SymbolDefinition? Symbol { get; set; }
    public FootprintDefinition? Footprint { get; set; }
    public StepModel? StepModel { get; set; }

    // = = = = = 构造函数 = = = = =

    /// <summary>4 参数：LcscId / Name / Description / Manufacturer（GUI 添加元件默认使用）</summary>
    public ComponentDetail(string lcscId, string name, string description, string manufacturer)
    {
        LcscId = lcscId;
        Name = name;
        Description = description;
        Manufacturer = manufacturer;
        Package = string.Empty;
    }

    /// <summary>6 参数：LcscId / Name / Description / Manufacturer / SymbolJson / FootprintJson（API 搜索后详情使用）</summary>
    public ComponentDetail(string lcscId, string name, string description, string manufacturer, string? symbolJson, string? footprintJson)
    {
        LcscId = lcscId;
        Name = name;
        Description = description;
        Manufacturer = manufacturer;
        Package = string.Empty;
        SymbolJson = symbolJson;
        FootprintJson = footprintJson;
    }

    /// <summary>原始 record 风格构造函数：复杂对象参数（保持向后兼容）</summary>
    public ComponentDetail(string lcscId, string name, string description, SymbolDefinition? symbol, FootprintDefinition? footprint, StepModel? stepModel)
    {
        LcscId = lcscId;
        Name = name;
        Description = description;
        Manufacturer = string.Empty;
        Package = string.Empty;
        Symbol = symbol;
        Footprint = footprint;
        StepModel = stepModel;
    }
}

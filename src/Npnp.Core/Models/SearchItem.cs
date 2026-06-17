using System.Collections.Generic;

namespace Npnp.Core.Models;

/// <summary>
/// 搜索项 - 搜索结果中的单个元件条目
/// </summary>
public class SearchItem
{
    // = = = = = 基础字段（与旧 record 兼容） = = = = =
    public string LcscId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public string Package { get; set; }
    public string Manufacturer { get; set; }

    // = = = = = 扩展字段 = = = = =
    public string? LcscPartName { get; set; }          // LCSC 元件规格名
    public string? ManufacturerPart { get; set; }       // 厂家型号
    public string? Supplier { get; set; }                // 供应商
    public string? PartClass { get; set; }               // 元件类别（Basic Part 等）
    public string? DatasheetUrl { get; set; }               // 数据手册链接
    public string? ImageUrl { get; set; }                // 产品图片
    public string? Designator { get; set; }                 // 位号前缀（如 R?）
    public string? Uuid { get; set; }                       // 元件整体 UUID
    public string? SymbolUuid { get; set; }                 // 符号 UUID
    public string? FootprintUuid { get; set; }              // 封装 UUID
    public string? Model3DUuid { get; set; }               // 3D 模型 UUID
    public string? DisplayTitle { get; set; }               // 显示标题

    // = = = = = 电气参数 = = = = =
    public string? Value { get; set; }                      // 数值（如 1MΩ）
    public string? Tolerance { get; set; }              // 精度（如 ±1%）
    public string? Power { get; set; }                    // 功率（如 250mW）
    public string? Voltage { get; set; }                   // 电压（如 50V）
    public string? TemperatureCoefficient { get; set; }    // 温度系数
    public string? OperatingTemperature { get; set; }      // 工作温度
    public string? ComponentType { get; set; }            // 元件类型
    public string? Category { get; set; }                      // 类别（如 电阻）
    public string? SubCategory { get; set; }                // 子类别（如 贴片电阻）
    public string? SpecSummary { get; set; }             // 规格摘要（综合文本）
    public string? SymbolJson { get; set; }              // 符号 JSON（从详情页获取）
    public string? FootprintJson { get; set; }           // 封装 JSON（从详情页获取）
    public Dictionary<string, string> AllAttributes { get; set; } = new();

    // = = = = = 构造函数 = = = = =

    /// <summary>5 参数构造函数（LcscId / Name / Description / Package / Manufacturer）- API 搜索默认使用</summary>
    public SearchItem(string lcscId, string name, string description, string package, string manufacturer)
    {
        LcscId = lcscId;
        Name = name;
        Description = description;
        Package = package;
        Manufacturer = manufacturer;
    }

    /// <summary>4 参数构造函数（LcscId / Name / Description / Manufacturer）- CLI 搜索回退使用</summary>
    public SearchItem(string lcscId, string name, string description, string manufacturer)
    {
        LcscId = lcscId;
        Name = name;
        Description = description;
        Package = string.Empty;
        Manufacturer = manufacturer;
    }
}

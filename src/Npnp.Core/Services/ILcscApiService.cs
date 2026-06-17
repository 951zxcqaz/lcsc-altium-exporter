using Npnp.Core.Models;

namespace Npnp.Core.Services;

/// <summary>
/// EasyEDA 源数据记录
/// </summary>
public record EasyEdaSource(string Uuid, string SymbolJson, string FootprintJson);

/// <summary>
/// LCSC API 服务接口
/// </summary>
public interface ILcscApiService
{
    /// <summary>
    /// 搜索元件（模糊关键词搜索）
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="limit">返回结果数量限制</param>
    /// <returns>搜索结果</returns>
    Task<SearchResult> SearchAsync(string keyword, int limit = 10);

    /// <summary>
    /// 按单个字段搜索元件
    /// </summary>
    /// <param name="keyword">搜索关键词</param>
    /// <param name="field">字段名（lcscId/name/manufacturerPart/manufacturer/package/supplier/value/spec）</param>
    /// <param name="limit">返回结果数量限制</param>
    /// <returns>搜索结果</returns>
    Task<SearchResult> SearchByFieldAsync(string keyword, string field, int limit = 10);

    /// <summary>
    /// 多字段联合搜索
    /// </summary>
    /// <param name="fields">字段字典（key=字段名，value=搜索值）</param>
    /// <param name="limit">每个字段的搜索结果数量限制</param>
    /// <returns>搜索结果（所有字段匹配结果的并集，按 LCSC ID 去重）</returns>
    Task<SearchResult> SearchByUnionAsync(Dictionary<string, string> fields, int limit = 10);

    /// <summary>
    /// 获取元件详情
    /// </summary>
    /// <param name="lcscId">LCSC 元件编号</param>
    /// <returns>元件详情，未找到时返回 null</returns>
    Task<ComponentDetail?> GetComponentAsync(string lcscId);

    /// <summary>
    /// 通过 symbol UUID 获取 symbol 原始 dataStr
    /// </summary>
    /// <param name="symbolUuid">symbol UUID</param>
    /// <returns>EasyEDA 格式的符号 dataStr，未找到时返回 null</returns>
    Task<string?> GetSymbolDataStrAsync(string symbolUuid);

    /// <summary>
    /// 通过 footprint UUID 获取 footprint 原始 dataStr
    /// </summary>
    /// <param name="footprintUuid">footprint UUID</param>
    /// <returns>EasyEDA 格式的封装 dataStr，未找到时返回 null</returns>
    Task<string?> GetFootprintDataStrAsync(string footprintUuid);

    /// <summary>
    /// 获取指定 UUID 的 EasyEDA 原始 JSON 响应（用于调试和获取 dataStr）
    /// </summary>
    /// <param name="uuid">组件 UUID</param>
    /// <returns>原始 JSON 字符串</returns>
    Task<string?> GetRawComponentJsonAsync(string uuid);

    /// <summary>
    /// 获取 EasyEDA 源数据
    /// </summary>
    /// <param name="uuid">EasyEDA 组件 UUID</param>
    /// <returns>EasyEDA 源数据，未找到时返回 null</returns>
    Task<EasyEdaSource?> GetEasyEdaSourceAsync(string uuid);

    /// <summary>
    /// 下载 STEP 模型
    /// </summary>
    /// <param name="url">模型下载地址</param>
    /// <returns>模型二进制数据，下载失败返回 null</returns>
    Task<byte[]?> DownloadStepModelAsync(string url);

    /// <summary>
    /// 下载 OBJ 模型
    /// </summary>
    /// <param name="url">模型下载地址</param>
    /// <returns>模型二进制数据，下载失败返回 null</returns>
    Task<byte[]?> DownloadObjModelAsync(string url);
}
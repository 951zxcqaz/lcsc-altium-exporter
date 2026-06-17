using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Npnp.Core.Models;
using Polly;
using Polly.Retry;

namespace Npnp.Core.Services;

public class LcscApiService : ILcscApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<LcscApiService>? _logger;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly ConcurrentDictionary<string, string> _cookies = new();

    private const string SearchApiUrl = "https://pro.lceda.cn/api/szlcsc/eda/product/list";
    private const string SearchApiUrlAlt = "https://so.szlcsc.com/search/global";
    private const string ComponentApiUrl = "https://pro.lceda.cn/api/components";
    private const string ModulesBaseUrl = "https://modules.lceda.cn/qAxj6KHrDKw4blvCG8QJPs7Y";

    public LcscApiService(HttpClient httpClient, ILogger<LcscApiService>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        // HttpClient 的 headers 已在 DI 注册时配置，这里不再重复设置

        _retryPolicy = Policy<HttpResponseMessage>
            .Handle<HttpRequestException>()
            .OrResult(response => !response.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 2,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(1.5, retryAttempt)));
    }

    public void SetCookies(IEnumerable<KeyValuePair<string, string>> cookies)
    {
        _cookies.Clear();
        foreach (var kv in cookies)
        {
            _cookies[kv.Key] = kv.Value;
        }

        if (_cookies.Count > 0)
        {
            _httpClient.DefaultRequestHeaders.Remove("Cookie");
            var cookieHeader = string.Join("; ", _cookies.Select(kv => $"{kv.Key}={kv.Value}"));
            _httpClient.DefaultRequestHeaders.Add("Cookie", cookieHeader);
            _logger?.LogInformation("已设置 {Count} 个 Cookie 用于 API 请求", _cookies.Count);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Remove("Cookie");
            _logger?.LogInformation("已清除 API Cookie");
        }
    }

    public bool HasCookies => _cookies.Count > 0;

    public async Task<SearchResult> SearchAsync(string keyword, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            _logger?.LogWarning("搜索关键词为空，直接返回空结果");
            return new SearchResult(0, Array.Empty<SearchItem>());
        }

        var trimmedKeyword = keyword.Trim();
        _logger?.LogInformation("搜索元件: {Keyword}", trimmedKeyword);

        // ========== 双 API 并行搜索，合并所有结果去重 ==========
        // API 1: so.szlcsc.com（立创商城搜索，优先）
        // API 2: pro.lceda.cn（立创 EDA 元件搜索）
        // 两个 API 都搜，结果按编号去重后合并返回

        var allItems = new List<SearchItem>();
        var seenLcscIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 并行请求两个 API
        var edaTask = QueryApiAsync(SearchApiUrl, trimmedKeyword, limit);
        var mallTask = QueryApiAsync(SearchApiUrlAlt, trimmedKeyword, limit);

        await Task.WhenAll(edaTask, mallTask);

        var edaResult = edaTask.Result;
        var mallResult = mallTask.Result;

        _logger?.LogInformation("[EDA] 找到 {Count} 个结果 | [商城] 找到 {Count} 个结果",
            edaResult.Items.Count, mallResult.Items.Count);

        // 合并结果去重（优先保留商城数据，因为商城数据更完整）
        foreach (var item in mallResult.Items)
        {
            AddUniqueItem(item, seenLcscIds, allItems);
        }
        foreach (var item in edaResult.Items)
        {
            AddUniqueItem(item, seenLcscIds, allItems);
        }

        _logger?.LogInformation("合并后共 {Count} 个唯一结果", allItems.Count);
        return new SearchResult(allItems.Count, allItems);
    }

    public async Task<SearchResult> SearchByFieldAsync(string keyword, string field, int limit = 10)
    {
        if (string.IsNullOrWhiteSpace(keyword) || string.IsNullOrWhiteSpace(field))
            return new SearchResult(0, Array.Empty<SearchItem>());

        var trimmedKeyword = keyword.Trim();
        _logger?.LogInformation("按字段搜索: {Field} = {Keyword}", field, trimmedKeyword);

        // 直接用纯关键词搜索（API 不理解字段前缀，所以不添加）
        return await SearchAsync(trimmedKeyword, limit);
    }

    public async Task<SearchResult> SearchByUnionAsync(Dictionary<string, string> fields, int limit = 10)
    {
        if (fields == null || fields.Count == 0)
            return new SearchResult(0, Array.Empty<SearchItem>());

        _logger?.LogInformation("联合搜索，字段数量: {Count}", fields.Count);

        var totalFields = fields.Count;
        if (totalFields == 0)
            return new SearchResult(0, Array.Empty<SearchItem>());

        // 存储每个 LCSC ID 在哪些字段搜索中命中了
        var hitCountByLcscId = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var itemsByLcscId = new Dictionary<string, SearchItem>(StringComparer.OrdinalIgnoreCase);

        // 并行按每个字段搜索，记录命中情况
        var tasks = new List<Task>();
        foreach (var kv in fields)
        {
            var field = kv.Key;
            var value = kv.Value.Trim();
            tasks.Add(SearchAsync(value, limit).ContinueWith(task =>
            {
                if (task.Result.Items != null)
                {
                    foreach (var item in task.Result.Items)
                    {
                        if (string.IsNullOrEmpty(item.LcscId)) continue;

                        // 记录这个 LCSC ID 在该字段命中
                        if (!hitCountByLcscId.ContainsKey(item.LcscId))
                        {
                            hitCountByLcscId[item.LcscId] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        }
                        hitCountByLcscId[item.LcscId].Add(field);

                        // 保存完整的 item 信息（保留第一次命中的）
                        if (!itemsByLcscId.ContainsKey(item.LcscId))
                        {
                            itemsByLcscId[item.LcscId] = item;
                        }
                    }
                }
            }));
        }
        await Task.WhenAll(tasks);

        // ========== 多级匹配策略 ==========
        var results = new List<SearchItem>();

        // 第一级：所有条件都完全匹配
        var perfectMatches = itemsByLcscId.Where(kv => hitCountByLcscId[kv.Key].Count == totalFields)
                                           .Select(kv => kv.Value)
                                           .Take(limit)
                                           .ToList();
        
        if (perfectMatches.Count > 0)
        {
            _logger?.LogInformation("联合搜索 - 找到 {Count} 个完全匹配结果", perfectMatches.Count);
            results.AddRange(perfectMatches);
        }
        else
        {
            // 第二级：允许最多1个条件不匹配
            var partialMatches = itemsByLcscId.Where(kv => hitCountByLcscId[kv.Key].Count >= totalFields - 1)
                                              .Select(kv => kv.Value)
                                              .Take(limit)
                                              .ToList();
            
            if (partialMatches.Count > 0)
            {
                _logger?.LogInformation("联合搜索 - 无完全匹配，找到 {Count} 个允许1个条件不匹配的结果", partialMatches.Count);
                results.AddRange(partialMatches);
            }
            else
            {
                _logger?.LogInformation("联合搜索 - 无匹配结果");
                return new SearchResult(0, Array.Empty<SearchItem>());
            }
        }

        _logger?.LogInformation("联合搜索完成，共 {Count} 个结果", results.Count);
        return new SearchResult(results.Count, results);
    }

    private static void AddUniqueItem(SearchItem item, HashSet<string> seenIds, List<SearchItem> items)
    {
        if (string.IsNullOrEmpty(item.LcscId)) { items.Add(item); return; }
        if (seenIds.Add(item.LcscId)) items.Add(item);
        else
        {
            // 编号重复：替换已有项（更新字段）
            var idx = items.FindIndex(x => x.LcscId?.Equals(item.LcscId, StringComparison.OrdinalIgnoreCase) == true);
            if (idx >= 0) items[idx] = item;
        }
    }

    // 统一的 API 查询方法：尝试多种 JSON 响应格式
    private async Task<SearchResult> QueryApiAsync(string apiUrl, string keyword, int limit)
    {
        var items = new List<SearchItem>();
        var seenLcscIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var url = $"{apiUrl}?wd={Uri.EscapeDataString(keyword)}&limit={limit}";
            _logger?.LogDebug("请求 URL: {Url}", url);

            var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(url));
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("API 返回状态码: {StatusCode}", (int)response.StatusCode);
                return new SearchResult(0, items);
            }

            var jsonContent = await response.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(jsonContent))
            {
                return new SearchResult(0, items);
            }

            // 尝试 EDA API 的标准解析
            var result = ParseSearchResult(jsonContent, limit);
            foreach (var item in result.Items)
            {
                if (!string.IsNullOrEmpty(item.LcscId) && seenLcscIds.Add(item.LcscId))
                    items.Add(item);
            }

            // 如果 EDA 格式没找到，尝试商城格式
            if (items.Count == 0)
            {
                var altResult = ParseSearchResultAlt(jsonContent, limit);
                foreach (var item in altResult.Items)
                {
                    if (!string.IsNullOrEmpty(item.LcscId) && seenLcscIds.Add(item.LcscId))
                        items.Add(item);
                }
            }

            // 如果前两种都没找到但 HTML 页面，尝试 HTML 解析
            if (items.Count == 0 && jsonContent.TrimStart().StartsWith("<", StringComparison.Ordinal))
            {
                var htmlResult = ParseHtmlSearchPage(jsonContent, keyword);
                foreach (var item in htmlResult.Items)
                {
                    if (!string.IsNullOrEmpty(item.LcscId) && seenLcscIds.Add(item.LcscId))
                        items.Add(item);
                }
            }

            _logger?.LogDebug("API 查询完成，共 {Count} 个唯一结果", items.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "API 查询失败: {ApiUrl}", apiUrl);
        }

        return new SearchResult(items.Count, items);
    }

    public async Task<ComponentDetail?> GetComponentAsync(string uuid)
    {
        _logger?.LogInformation("获取元件详情: {Uuid}", uuid);

        if (!HasCookies)
        {
            _logger?.LogWarning("获取元件详情需要登录 Cookie");
        }

        var url = $"{ComponentApiUrl}/{uuid}?uuid={uuid}";
        var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(url));

        if (!response.IsSuccessStatusCode)
        {
            _logger?.LogWarning("元件详情 API 返回状态码: {StatusCode}", (int)response.StatusCode);
            return null;
        }

        var jsonContent = await response.Content.ReadAsStringAsync();
        return ParseComponentDetail(jsonContent, uuid);
    }

    public async Task<EasyEdaSource?> GetEasyEdaSourceAsync(string uuid)
    {
        _logger?.LogInformation("获取 EasyEDA 源数据: {Uuid}", uuid);

        var detail = await GetComponentAsync(uuid);
        if (detail == null)
        {
            return null;
        }

        return new EasyEdaSource(uuid, detail.SymbolJson ?? string.Empty, detail.FootprintJson ?? string.Empty);
    }

    /// <summary>
    /// 通过 symbol UUID 获取 symbol dataStr
    /// </summary>
    public async Task<string?> GetSymbolDataStrAsync(string symbolUuid)
    {
        if (string.IsNullOrEmpty(symbolUuid)) return null;
        var raw = await GetRawComponentJsonAsync(symbolUuid);
        if (raw == null) return null;
        return ExtractDataStrFromComponentJson(raw);
    }

    /// <summary>
    /// 通过 footprint UUID 获取 footprint dataStr
    /// </summary>
    public async Task<string?> GetFootprintDataStrAsync(string footprintUuid)
    {
        if (string.IsNullOrEmpty(footprintUuid)) return null;
        var raw = await GetRawComponentJsonAsync(footprintUuid);
        if (raw == null) return null;
        return ExtractDataStrFromComponentJson(raw);
    }

    /// <summary>
    /// 从 component API 响应 JSON 中提取 dataStr 字段
    /// </summary>
    private static string? ExtractDataStrFromComponentJson(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Object)
            {
                if (result.TryGetProperty("dataStr", out var dsEl) && dsEl.ValueKind == JsonValueKind.String)
                {
                    return dsEl.GetString();
                }
            }
        }
        catch
        {
            // 忽略解析错误
        }
        return null;
    }

    /// <summary>
    /// 直接获取指定 UUID 的 EasyEDA 原始 JSON 响应（用于调试和获取 dataStr）
    /// </summary>
    public async Task<string?> GetRawComponentJsonAsync(string uuid)
    {
        var url = $"{ComponentApiUrl}/{uuid}?uuid={uuid}";
        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(url));
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("元件详情 API 返回状态码: {StatusCode}", (int)response.StatusCode);
                return null;
            }
            return await response.Content.ReadAsStringAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "获取元件原始 JSON 异常: {Uuid}", uuid);
            return null;
        }
    }

    public async Task<byte[]?> DownloadStepModelAsync(string modelUuid)
    {
        _logger?.LogInformation("下载 STEP 模型: {Uuid}", modelUuid);

        var url = $"{ModulesBaseUrl}/{modelUuid}";
        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(url));
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("3D 模型下载失败 (HTTP {StatusCode}): {Url}", (int)response.StatusCode, url);
                return null;
            }

            var data = await response.Content.ReadAsByteArrayAsync();
            _logger?.LogInformation("下载 3D 模型成功, 大小 {Size} bytes", data.Length);
            return data;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "3D 模型下载异常: {Url}", url);
            return null;
        }
    }

    public async Task<byte[]?> DownloadObjModelAsync(string modelUuid)
    {
        _logger?.LogInformation("下载 OBJ 模型: {Uuid}", modelUuid);

        var url = $"https://modules.lceda.cn/3dmodel/{modelUuid}";
        try
        {
            var response = await _retryPolicy.ExecuteAsync(async () => await _httpClient.GetAsync(url));
            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogWarning("3D 模型(OBJ)下载失败 (HTTP {StatusCode}): {Url}", (int)response.StatusCode, url);
                return null;
            }

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "3D 模型(OBJ)下载异常: {Url}", url);
            return null;
        }
    }

    private static SearchResult ParseSearchResult(string jsonContent, int limit)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        if (!root.TryGetProperty("success", out var successProp) ||
            successProp.ValueKind != JsonValueKind.True)
        {
            return new SearchResult(0, Array.Empty<SearchItem>());
        }

        if (!root.TryGetProperty("result", out var resultProp) ||
            resultProp.ValueKind != JsonValueKind.Array)
        {
            return new SearchResult(0, Array.Empty<SearchItem>());
        }

        var items = new List<SearchItem>();
        int index = 1;

        foreach (var item in resultProp.EnumerateArray())
        {
            if (index > limit) break;

            // ============== 从 attributes 提取所有字段（优先从 attributes，因为顶层字段经常为空）==============
            var allAttributes = new Dictionary<string, string>();
            string? lcscId = null;
            string? lcscPartName = null;
            string? manufacturer = null;
            string? manufacturerPart = null;
            string? supplier = null;
            string? partClass = null;
            string? datasheetUrl = null;
            string? designator = null;
            string? value = null;
            string? tolerance = null;
            string? power = null;
            string? voltage = null;
            string? tempCoeff = null;
            string? operatingTemp = null;
            string? componentType = null;
            string? symbolUuid = null;
            string? footprintUuid = null;
            string? model3dUuid = null;
            string? supplierFootprint = null;

            if (item.TryGetProperty("attributes", out var attrsProp) &&
                attrsProp.ValueKind == JsonValueKind.Object)
            {
                foreach (var attr in attrsProp.EnumerateObject())
                {
                    var val = attr.Value.GetString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        allAttributes[attr.Name] = val;
                    }
                }

                lcscPartName = GetDictValue(allAttributes, "LCSC Part Name");
                manufacturerPart = GetDictValue(allAttributes, "Manufacturer Part");
                manufacturer = GetDictValue(allAttributes, "Manufacturer");
                supplier = GetDictValue(allAttributes, "Supplier");
                partClass = GetDictValue(allAttributes, "JLCPCB Part Class");
                datasheetUrl = GetDictValue(allAttributes, "Datasheet");
                designator = GetDictValue(allAttributes, "Designator");
                value = GetDictValue(allAttributes, "Value");
                tolerance = GetDictValue(allAttributes, "Tolerance");
                power = GetDictValue(allAttributes, "Power(Watts)") ?? GetDictValue(allAttributes, "Power");
                // voltage 字段名有多种变体
                voltage = GetDictValue(allAttributes, "Voltage Rating") ??
                         GetDictValue(allAttributes, "Voltage-Supply(Max)") ??
                         GetDictValue(allAttributes, "Voltage") ??
                         GetDictValue(allAttributes, "Rated Voltage");
                tempCoeff = GetDictValue(allAttributes, "Temperature Coefficient");
                operatingTemp = GetDictValue(allAttributes, "Operating Temperature");
                componentType = GetDictValue(allAttributes, "Type");
                symbolUuid = GetDictValue(allAttributes, "Symbol");
                footprintUuid = GetDictValue(allAttributes, "Footprint");
                model3dUuid = GetDictValue(allAttributes, "3D Model");
                supplierFootprint = GetDictValue(allAttributes, "Supplier Footprint");
            }

            // ============== 从顶层字段补充（如果 attributes 中没有）==============
            lcscId = GetStringProperty(item, "product_code") ?? GetDictValue(allAttributes, "Supplier Part");

            // 如果 product_code 仍然为空，尝试其他字段名
            if (string.IsNullOrEmpty(lcscId))
            {
                lcscId = GetStringProperty(item, "supplier_part");
            }

            // uuid
            var uuid = GetStringProperty(item, "uuid");

            // name: 优先用 display_title（大写/有格式），回退到 title（小写）
            var displayTitle = GetStringProperty(item, "display_title");
            var title = GetStringProperty(item, "title");
            var name = displayTitle ?? title ?? lcscPartName ?? string.Empty;

            // description
            var description = GetStringProperty(item, "description") ?? string.Empty;

            // manufacturer 回退到顶层字段
            if (string.IsNullOrEmpty(manufacturer))
                manufacturer = GetStringProperty(item, "manufacturer");

            // 从顶层 symbol/footprint 对象中获取 uuid
            if (string.IsNullOrEmpty(symbolUuid) &&
                item.TryGetProperty("symbol", out var symbolProp) &&
                symbolProp.ValueKind == JsonValueKind.Object)
            {
                symbolUuid = GetStringProperty(symbolProp, "uuid");
            }

            if (string.IsNullOrEmpty(footprintUuid) &&
                item.TryGetProperty("footprint", out var footprintProp) &&
                footprintProp.ValueKind == JsonValueKind.Object)
            {
                footprintUuid = GetStringProperty(footprintProp, "uuid");
            }

            // package: 优先用 Supplier Footprint，其次是 footprint 对象的标题
            var package = supplierFootprint ??
                         GetStringProperty(item, "footprint", "display_title") ??
                         GetStringProperty(item, "footprint", "title") ??
                         string.Empty;

            // images
            string? imageUrl = null;
            if (item.TryGetProperty("images", out var imagesProp) &&
                imagesProp.ValueKind == JsonValueKind.Array)
            {
                var en = imagesProp.EnumerateArray();
                if (en.Any())
                {
                    imageUrl = en.First().GetString();
                }
            }

            // 类别
            string? category = null;
            string? subCategory = null;
            if (item.TryGetProperty("tags", out var tagsProp) &&
                tagsProp.ValueKind == JsonValueKind.Object)
            {
                if (tagsProp.TryGetProperty("parent_tag", out var parentTag) &&
                    parentTag.ValueKind == JsonValueKind.Object)
                {
                    category = GetStringProperty(parentTag, "name_cn") ??
                               GetStringProperty(parentTag, "name");
                }
                if (tagsProp.TryGetProperty("child_tag", out var childTag) &&
                    childTag.ValueKind == JsonValueKind.Object)
                {
                    subCategory = GetStringProperty(childTag, "name_cn") ??
                                  GetStringProperty(childTag, "name");
                }
            }

            // lcscId 最后回退
            if (string.IsNullOrEmpty(lcscId))
            {
                lcscId = symbolUuid ?? footprintUuid ?? string.Empty;
            }

            // 规格摘要（包含更多信息便于搜索）
            var specParts = new List<string>();
            if (!string.IsNullOrEmpty(value)) specParts.Add(value);
            if (!string.IsNullOrEmpty(tolerance)) specParts.Add(tolerance);
            if (!string.IsNullOrEmpty(voltage)) specParts.Add(voltage);
            if (!string.IsNullOrEmpty(power)) specParts.Add(power);
            if (!string.IsNullOrEmpty(package)) specParts.Add(package);
            var specSummary = string.Join(" | ", specParts);

            var searchItem = new SearchItem(
                lcscId: lcscId ?? string.Empty,
                name: name,
                description: description,
                package: package,
                manufacturer: manufacturer ?? string.Empty
            )
            {
                Uuid = uuid,
                SymbolUuid = symbolUuid,
                FootprintUuid = footprintUuid,
                Model3DUuid = model3dUuid,
                DisplayTitle = displayTitle,
                LcscPartName = lcscPartName,
                ManufacturerPart = manufacturerPart,
                Supplier = supplier,
                PartClass = partClass,
                DatasheetUrl = datasheetUrl,
                ImageUrl = imageUrl,
                Designator = designator,
                Value = value,
                Tolerance = tolerance,
                Power = power,
                Voltage = voltage,
                TemperatureCoefficient = tempCoeff,
                OperatingTemperature = operatingTemp,
                ComponentType = componentType,
                Category = category,
                SubCategory = subCategory,
                AllAttributes = allAttributes,
                SpecSummary = specSummary
            };

            items.Add(searchItem);
            index++;
        }

        return new SearchResult(items.Count, items);
    }

    // 解析立创商城搜索 API 的响应（尝试多种 JSON 结构）
    private static SearchResult ParseSearchResultAlt(string jsonContent, int limit)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // 尝试找到一个数组字段作为结果列表
            // 支持的结构: { result: [...] }, { data: { list: [...] } }, { products: [...] }, [...]
            JsonElement arrayElement = default;
            bool found = false;

            if (root.TryGetProperty("result", out var resultProp) && resultProp.ValueKind == JsonValueKind.Array)
            { arrayElement = resultProp; found = true; }
            else if (root.TryGetProperty("data", out var dataProp))
            {
                if (dataProp.TryGetProperty("list", out var listProp) && listProp.ValueKind == JsonValueKind.Array)
                { arrayElement = listProp; found = true; }
                else if (dataProp.ValueKind == JsonValueKind.Array)
                { arrayElement = dataProp; found = true; }
            }
            else if (root.TryGetProperty("products", out var productsProp) && productsProp.ValueKind == JsonValueKind.Array)
            { arrayElement = productsProp; found = true; }
            else if (root.ValueKind == JsonValueKind.Array)
            { arrayElement = root; found = true; }

            if (!found) return new SearchResult(0, Array.Empty<SearchItem>());

            var items = new List<SearchItem>();
            int idx = 0;
            foreach (var item in arrayElement.EnumerateArray())
            {
                if (idx++ >= limit) break;

                var lcscId = GetStringProperty(item, "product_code") ??
                             GetStringProperty(item, "supplier_part") ??
                             GetStringProperty(item, "lcscId");

                var name = GetStringProperty(item, "display_title") ??
                          GetStringProperty(item, "title") ??
                          GetStringProperty(item, "name") ?? string.Empty;

                if (string.IsNullOrEmpty(lcscId) && string.IsNullOrEmpty(name))
                    continue;

                var manufacturer = GetStringProperty(item, "manufacturer");
                var manufacturerPart = GetStringProperty(item, "manufacturer_part") ??
                                      GetStringProperty(item, "manufacturerPart");
                var package = GetStringProperty(item, "footprint", "display_title") ??
                             GetStringProperty(item, "footprint", "title") ??
                             GetStringProperty(item, "package") ?? string.Empty;

                var searchItem = new SearchItem(
                    lcscId: lcscId ?? string.Empty,
                    name: name,
                    description: GetStringProperty(item, "description") ?? string.Empty,
                    package: package,
                    manufacturer: manufacturer ?? string.Empty
                );
                searchItem.ManufacturerPart = manufacturerPart;
                items.Add(searchItem);
            }

            return new SearchResult(items.Count, items);
        }
        catch
        {
            return new SearchResult(0, Array.Empty<SearchItem>());
        }
    }

    // 从 HTML 搜索页面中提取 LCSC 编号（当 API 返回 HTML 时用，保持轻量）
    private static SearchResult ParseHtmlSearchPage(string htmlContent, string keyword)
    {
        var items = new List<SearchItem>();
        try
        {
            // 只从 HTML 中提取形如 "C12345" 的 LCSC 编号
            // 这样避免了复杂的 JSON 解析，也不会引入误匹配
            var lcscIdMatches = System.Text.RegularExpressions.Regex.Matches(
                htmlContent, @"\b[Cc][0-9]{4,10}\b");

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in lcscIdMatches)
            {
                if (seenIds.Add(m.Value))
                {
                    items.Add(new SearchItem(
                        lcscId: m.Value.ToUpperInvariant(),
                        name: m.Value.ToUpperInvariant(),
                        description: string.Empty,
                        package: string.Empty,
                        manufacturer: string.Empty
                    ));
                }
                if (items.Count >= 5) break;
            }
        }
        catch
        {
            // HTML 解析失败
        }

        return new SearchResult(items.Count, items);
    }

    private static string? GetDictValue(Dictionary<string, string> dict, string key)
    {
        if (dict.TryGetValue(key, out var v) && !string.IsNullOrEmpty(v))
            return v;
        return null;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }
        
        if (prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static string? GetStringProperty(JsonElement element, string parentName, string childName)
    {
        if (!element.TryGetProperty(parentName, out var parent) || 
            parent.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        return GetStringProperty(parent, childName);
    }

    private static ComponentDetail? ParseComponentDetail(string jsonContent, string uuid)
    {
        using var doc = JsonDocument.Parse(jsonContent);
        var root = doc.RootElement;

        // 检查成功状态
        if (!root.TryGetProperty("success", out var successProp) || 
            successProp.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        // 获取结果
        if (!root.TryGetProperty("result", out var resultProp) || 
            resultProp.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var lcscId = GetStringProperty(resultProp, "product_code") ?? uuid;
        var name = GetStringProperty(resultProp, "display_title") ?? 
                   GetStringProperty(resultProp, "title") ?? uuid;
        var description = GetStringProperty(resultProp, "description") ?? string.Empty;
        var manufacturer = GetStringProperty(resultProp, "manufacturer") ?? string.Empty;

        // 获取 symbol 和 footprint JSON - npnp 使用 dataStr 字段
        // API 返回的结构可能是 { dataStr: "PIN\nLINE\n...", symbol: {...}, footprint: {...} }
        // 或者 { symbol_json_str: "...", footprint_json_str: "..." }
        var symbolJson = GetStringProperty(resultProp, "dataStr") ?? 
                        GetStringProperty(resultProp, "symbol_json_str") ?? 
                        GetStringProperty(resultProp, "symbol") ?? 
                        string.Empty;
        var footprintJson = GetStringProperty(resultProp, "footprint_dataStr") ??
                           GetStringProperty(resultProp, "footprint_json_str") ?? 
                           GetStringProperty(resultProp, "footprint") ?? 
                           string.Empty;

        // 如果 symbolJson 为空但有 symbol 对象，尝试从中提取 dataStr
        if (string.IsNullOrEmpty(symbolJson) && 
            resultProp.TryGetProperty("symbol", out var symbolProp) && 
            symbolProp.ValueKind == JsonValueKind.Object)
        {
            symbolJson = GetStringProperty(symbolProp, "dataStr") ?? 
                        GetStringProperty(symbolProp, "uuid");
        }

        // 如果 footprintJson 为空但有 footprint 对象，尝试从中提取 dataStr
        if (string.IsNullOrEmpty(footprintJson) && 
            resultProp.TryGetProperty("footprint", out var footprintProp) && 
            footprintProp.ValueKind == JsonValueKind.Object)
        {
            footprintJson = GetStringProperty(footprintProp, "dataStr") ?? 
                           GetStringProperty(footprintProp, "uuid");
        }

        // 获取 UUID
        string? symbolUuid = null;
        string? footprintUuid = null;
        string? model3dUuid = null;

        if (resultProp.TryGetProperty("symbol", out var symProp) && 
            symProp.ValueKind == JsonValueKind.Object)
        {
            symbolUuid = GetStringProperty(symProp, "uuid");
            // 如果 symbolJson 仍为空，尝试从 symbol 对象获取 dataStr
            if (string.IsNullOrEmpty(symbolJson))
            {
                symbolJson = GetStringProperty(symProp, "dataStr");
            }
        }

        if (resultProp.TryGetProperty("footprint", out var fpProp) && 
            fpProp.ValueKind == JsonValueKind.Object)
        {
            footprintUuid = GetStringProperty(fpProp, "uuid");
            // 如果 footprintJson 仍为空，尝试从 footprint 对象获取 dataStr
            if (string.IsNullOrEmpty(footprintJson))
            {
                footprintJson = GetStringProperty(fpProp, "dataStr");
            }
        }

        // 尝试从 attributes 获取 3D Model UUID
        if (resultProp.TryGetProperty("attributes", out var attrsProp) && 
            attrsProp.ValueKind == JsonValueKind.Object)
        {
            model3dUuid = GetStringProperty(attrsProp, "3D Model");
        }

        // 尝试从 result 中获取 3D Model UUID
        if (string.IsNullOrEmpty(model3dUuid))
        {
            model3dUuid = GetStringProperty(resultProp, "3d_model_uuid");
        }

        return new ComponentDetail(
            lcscId,
            name,
            description,
            manufacturer,
            symbolJson,
            footprintJson
        )
        {
            SymbolUuid = symbolUuid,
            FootprintUuid = footprintUuid,
            Model3DUuid = model3dUuid
        };
    }
}

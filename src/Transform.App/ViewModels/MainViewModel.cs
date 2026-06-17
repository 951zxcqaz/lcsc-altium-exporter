#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Npnp.Core.Models;
using Npnp.Core.Services;
using Transform.App.Services;

namespace Transform.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILcscApiService? _lcscApiService;

    public MainViewModel()
    {
        ComponentListViewModel = new ComponentListViewModel();
        ExportSettingsViewModel = new ExportSettingsViewModel();
        ExportProgressViewModel = new ExportProgressViewModel();
    }

    public MainViewModel(ILcscApiService lcscApiService) : this()
    {
        _lcscApiService = lcscApiService;
    }

    public ComponentListViewModel ComponentListViewModel { get; }
    public ExportSettingsViewModel ExportSettingsViewModel { get; }
    public ExportProgressViewModel ExportProgressViewModel { get; }

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "就绪";

    [ObservableProperty]
    private string _selectedSearchFieldKey = "union";

    [ObservableProperty]
    private ObservableCollection<SearchItem> _searchResults = new();

    public List<SearchFieldOption> AvailableSearchFields { get; } = new()
    {
        new SearchFieldOption("联合搜索", "union", "多字段组合搜索，格式：名称:xxx 型号:xxx 品牌:xxx 封装:xxx 编号:xxx"),
        new SearchFieldOption("全部字段", "all", "在所有字段中模糊匹配关键词"),
        new SearchFieldOption("编号", "lcscId", "按 LCSC 元件编号搜索"),
        new SearchFieldOption("名称", "name", "按元件名称搜索"),
        new SearchFieldOption("型号", "manufacturerPart", "按厂家型号搜索"),
        new SearchFieldOption("品牌", "manufacturer", "按制造商品牌搜索"),
        new SearchFieldOption("封装", "package", "按封装搜索"),
        new SearchFieldOption("渠道", "supplier", "按供应商搜索"),
        new SearchFieldOption("数值", "value", "按元件数值搜索"),
        new SearchFieldOption("规格", "spec", "按规格摘要搜索"),
    };

    [ObservableProperty]
    private SearchItem? _selectedSearchItem;

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private bool _isExporting;

    [RelayCommand]
    public async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchKeyword))
        {
            StatusMessage = "请输入搜索关键词";
            return;
        }

        IsSearching = true;
        StatusMessage = "正在搜索...";
        SearchResults.Clear();

        try
        {
            if (SelectedSearchFieldKey == "union")
            {
                // 联合搜索：支持 名称:xxx 型号:xxx 品牌:xxx 封装:xxx 编号:xxx 格式
                var parsed = ParseUnionSearch(SearchKeyword);
                if (parsed.Count > 0)
                {
                    // 优先用编号搜索（最精确）
                    if (parsed.TryGetValue("lcscId", out var lcscId) && !string.IsNullOrWhiteSpace(lcscId))
                    {
                        var idResult = await _lcscApiService!.SearchAsync(lcscId, 10);
                        if (idResult.Items != null && idResult.Items.Count > 0)
                        {
                            foreach (var item in idResult.Items) SearchResults.Add(item);
                            StatusMessage = $"搜索完成，找到 {SearchResults.Count} 个结果（按编号匹配）";
                            return;
                        }
                    }

                    // 编号没找到，用其他字段联合搜索
                    var otherFields = parsed.Where(kv => kv.Key != "lcscId").ToDictionary(kv => kv.Key, kv => kv.Value);
                    if (otherFields.Count > 0)
                    {
                        var result = await _lcscApiService!.SearchByUnionAsync(otherFields, 50);
                        foreach (var item in result.Items) SearchResults.Add(item);
                    }
                    else
                    {
                        // 只有编号但没找到，尝试普通搜索
                        var result = await _lcscApiService!.SearchAsync(SearchKeyword, 50);
                        foreach (var item in result.Items) SearchResults.Add(item);
                    }
                }
                else
                {
                    // 没有解析到字段，回退为普通关键词搜索
                    var result = await _lcscApiService!.SearchAsync(SearchKeyword, 50);
                    foreach (var item in result.Items) SearchResults.Add(item);
                }
            }
            else if (SelectedSearchFieldKey == "all")
            {
                var result = await _lcscApiService!.SearchAsync(SearchKeyword, 50);
                foreach (var item in result.Items) SearchResults.Add(item);
            }
            else
            {
                var result = await _lcscApiService!.SearchByFieldAsync(SearchKeyword, SelectedSearchFieldKey, 50);
                foreach (var item in result.Items) SearchResults.Add(item);
            }

            StatusMessage = $"搜索完成，找到 {SearchResults.Count} 个结果";
        }
        catch (Exception ex)
        {
            StatusMessage = $"搜索失败: {ex.Message}";
        }
        finally
        {
            IsSearching = false;
        }
    }

    /// <summary>
    /// 解析联合搜索字符串，提取各字段的键值对
    /// 支持格式：名称:STM32F030C8T6 型号:STM32F030C8T6 品牌:ST 封装:LQFP-48 编号:C23922
    /// </summary>
    private Dictionary<string, string> ParseUnionSearch(string input)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(input)) return result;

        var normalized = input.Replace('：', ':').Trim();
        var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "编号", "lcscId" }, { "ID", "lcscId" }, { "LCSC", "lcscId" },
            { "名称", "name" }, { "元件名", "name" },
            { "型号", "manufacturerPart" }, { "厂家型号", "manufacturerPart" },
            { "品牌", "manufacturer" }, { "制造商", "manufacturer" },
            { "封装", "package" },
            { "渠道", "supplier" }, { "供应商", "supplier" },
            { "数值", "value" }, { "值", "value" },
            { "规格", "spec" }, { "参数", "spec" },
        };

        var regex = new System.Text.RegularExpressions.Regex(@"(?:^|\s)([\p{L}\p{N}]+):");
        var matches = new List<(int fieldStart, int colonEnd, string fieldName)>();
        foreach (System.Text.RegularExpressions.Match m in regex.Matches(normalized))
        {
            var fieldName = m.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(fieldName)) continue;
            matches.Add((m.Groups[1].Index, m.Groups[1].Index + m.Groups[1].Length + 1, fieldName));
        }
        if (matches.Count == 0) return result;

        for (int i = 0; i < matches.Count; i++)
        {
            var cur = matches[i];
            int valueStart = cur.colonEnd;
            while (valueStart < normalized.Length && char.IsWhiteSpace(normalized[valueStart])) valueStart++;
            int valueEnd = i + 1 < matches.Count ? matches[i + 1].fieldStart : normalized.Length;
            while (valueEnd > valueStart && char.IsWhiteSpace(normalized[valueEnd - 1])) valueEnd--;
            if (valueEnd <= valueStart) continue;

            var fieldValue = normalized.Substring(valueStart, valueEnd - valueStart).Trim();
            if (aliases.TryGetValue(cur.fieldName, out var fieldKey))
                result[fieldKey] = fieldValue;
        }
        return result;
    }
}

public record SearchFieldOption(string DisplayName, string Key, string Description);

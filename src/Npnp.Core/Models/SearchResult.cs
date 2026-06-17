namespace Npnp.Core.Models;

/// <summary>
/// 搜索结果记录类型
/// </summary>
public record SearchResult(int Total, IReadOnlyList<SearchItem> Items);
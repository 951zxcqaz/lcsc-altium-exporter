namespace Npnp.Core.Models;

/// <summary>
/// 导出选项记录类型
/// </summary>
public record ExportOptions(
    string LibraryName = "NpnpLibrary",
    bool EmbedStepModel = false,
    bool ForceOverwrite = false,
    bool MergeOutput = false,
    bool AppendMode = false);
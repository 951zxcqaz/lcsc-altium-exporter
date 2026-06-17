using System.Collections.Generic;

namespace Npnp.Core.Services;

/// <summary>
/// 封装 npnp CLI 工具调用的服务接口
/// </summary>
public interface INpnpCliService
{
    /// <summary>
    /// 检查 npnp.exe 是否存在
    /// </summary>
    bool IsAvailable();

    /// <summary>
    /// 获取 npnp.exe 的路径
    /// </summary>
    string GetExecutablePath();

    /// <summary>
    /// 批量导出 Altium 库文件 (SchLib + PcbLib)
    /// </summary>
    /// <param name="lcscIds">LCSC 元件 ID 列表</param>
    /// <param name="outputDir">输出目录</param>
    /// <param name="mergeOutput">是否合并输出</param>
    /// <param name="libraryName">库名称（合并输出时使用）</param>
    /// <param name="embedStepModel">是否嵌入 STEP 模型</param>
    /// <returns>导出结果</returns>
    NpnpExportResult BatchExport(
        IEnumerable<string> lcscIds,
        string outputDir,
        bool mergeOutput = false,
        string? libraryName = null,
        bool embedStepModel = false);
}

/// <summary>
/// npnp 导出结果
/// </summary>
public class NpnpExportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Output { get; set; }
    public int ExitCode { get; set; }
    public List<string> GeneratedFiles { get; set; } = new();
}

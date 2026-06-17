namespace Npnp.Core.Writers;

using Npnp.Core.Models;

/// <summary>
/// PcbLib 文件写入器接口
/// </summary>
public interface IPcbLibWriter
{
    /// <summary>
    /// 将元件写入到新的 PcbLib 文件
    /// </summary>
    /// <param name="outputPath">输出文件路径</param>
    /// <param name="components">要写入的元件集合</param>
    /// <param name="options">导出选项</param>
    void Write(string outputPath, IEnumerable<ComponentDetail> components, ExportOptions options);

    /// <summary>
    /// 将元件追加到现有的 PcbLib 文件
    /// </summary>
    /// <param name="outputPath">目标文件路径</param>
    /// <param name="components">要追加的元件集合</param>
    void Append(string outputPath, IEnumerable<ComponentDetail> components);
}
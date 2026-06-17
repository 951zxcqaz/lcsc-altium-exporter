namespace Npnp.Core.Services;

using Npnp.Core.Models;

/// <summary>
/// 导出状态枚举
/// </summary>
public enum ExportStatus
{
    /// <summary>
    /// 空闲状态
    /// </summary>
    Idle,

    /// <summary>
    /// 正在运行
    /// </summary>
    Running,

    /// <summary>
    /// 已完成
    /// </summary>
    Completed,

    /// <summary>
    /// 失败
    /// </summary>
    Failed,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled,

    /// <summary>
    /// 已取消（别名，兼容两种拼写）
    /// </summary>
    Canceled = Cancelled
}

/// <summary>
/// 导出进度记录类型
/// </summary>
public record ExportProgress(
    int Total,
    int Completed,
    int Failed,
    string CurrentComponent,
    ExportStatus Status);

/// <summary>
/// 导出服务接口
/// </summary>
public interface IExportService
{
    /// <summary>
    /// 异步导出元件到指定目录
    /// </summary>
    /// <param name="lcscIds">LCSC 元件编号集合</param>
    /// <param name="outputDirectory">输出目录</param>
    /// <param name="options">导出选项</param>
    Task ExportAsync(IEnumerable<string> lcscIds, string outputDirectory, ExportOptions options);

    /// <summary>
    /// 获取当前导出进度
    /// </summary>
    /// <returns>导出进度信息</returns>
    Task<ExportProgress> GetProgressAsync();
}
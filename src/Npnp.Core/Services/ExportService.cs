namespace Npnp.Core.Services;

using Npnp.Core.Models;
using Npnp.Core.Writers;

/// <summary>
/// 导出服务实现类
/// </summary>
public class ExportService : IExportService
{
    private readonly ILcscApiService _apiService;
    private readonly ISchLibWriter _schLibWriter;
    private readonly IPcbLibWriter _pcbLibWriter;
    private ExportProgress _progress;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="apiService">LCSC API 服务</param>
    /// <param name="schLibWriter">SchLib 写入器</param>
    /// <param name="pcbLibWriter">PcbLib 写入器</param>
    public ExportService(ILcscApiService apiService, ISchLibWriter schLibWriter, IPcbLibWriter pcbLibWriter)
    {
        _apiService = apiService;
        _schLibWriter = schLibWriter;
        _pcbLibWriter = pcbLibWriter;
        _progress = new ExportProgress(0, 0, 0, string.Empty, ExportStatus.Idle);
    }

    /// <summary>
    /// 异步导出元件到指定目录
    /// </summary>
    /// <param name="lcscIds">LCSC 元件编号集合</param>
    /// <param name="outputDirectory">输出目录</param>
    /// <param name="options">导出选项</param>
    public async Task ExportAsync(IEnumerable<string> lcscIds, string outputDirectory, ExportOptions options)
    {
        var lcscIdList = lcscIds.ToList();
        var total = lcscIdList.Count;
        var completed = 0;
        var failed = 0;
        var components = new List<ComponentDetail>();

        // 初始化进度状态
        _progress = new ExportProgress(total, 0, 0, string.Empty, ExportStatus.Running);

        try
        {
            // 创建输出目录
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            // 遍历 LCSC ID 列表，获取元件详情
            foreach (var lcscId in lcscIdList)
            {
                // 更新当前处理的元件
                _progress = _progress with { CurrentComponent = lcscId };

                try
                {
                    var component = await _apiService.GetComponentAsync(lcscId);
                    if (component != null)
                    {
                        components.Add(component);
                        completed++;
                    }
                    else
                    {
                        failed++;
                    }
                }
                catch
                {
                    failed++;
                }

                // 更新进度
                _progress = _progress with { Completed = completed, Failed = failed };
            }

            // 导出 SchLib 文件
            var schLibPath = Path.Combine(outputDirectory, $"{options.LibraryName}.SchLib");
            _schLibWriter.Write(schLibPath, components, options);

            // 导出 PcbLib 文件
            var pcbLibPath = Path.Combine(outputDirectory, $"{options.LibraryName}.PcbLib");
            _pcbLibWriter.Write(pcbLibPath, components, options);

            // 设置完成状态
            _progress = _progress with { Status = ExportStatus.Completed, CurrentComponent = string.Empty };
        }
        catch
        {
            // 设置失败状态
            _progress = _progress with { Status = ExportStatus.Failed };
            throw;
        }
    }

    /// <summary>
    /// 获取当前导出进度
    /// </summary>
    /// <returns>导出进度信息</returns>
    public Task<ExportProgress> GetProgressAsync()
    {
        return Task.FromResult(_progress);
    }
}
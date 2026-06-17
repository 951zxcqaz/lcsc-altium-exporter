namespace Npnp.Core.Services;

using System.Diagnostics;
using System.Text;
using Npnp.Core.Models;

/// <summary>
/// 调用 npnp CLI 进行导出的服务
/// 如果 npnp 已安装，可以直接使用它进行 Altium 库文件导出
/// </summary>
public class NpnpRunner
{
    private readonly string? _npnpPath;
    private readonly List<string> _logMessages = new();

    public NpnpRunner()
    {
        _npnpPath = FindNpnpExecutable();
    }

    public bool IsAvailable => !string.IsNullOrEmpty(_npnpPath);

    private string? FindNpnpExecutable()
    {
        // 可能的 npnp 可执行文件位置
        var possiblePaths = new[]
        {
            // 用户目录下的 .cargo/bin
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "npnp.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cargo", "bin", "npnp"),
            
            // 系统 PATH 中的 npnp
            "npnp",
            "npnp.exe",
            
            // 当前目录
            Path.Combine(AppContext.BaseDirectory, "npnp.exe"),
            Path.Combine(AppContext.BaseDirectory, "npnp"),
        };

        foreach (var path in possiblePaths)
        {
            if (File.Exists(path))
            {
                _logMessages.Add($"找到 npnp: {path}");
                return path;
            }
        }

        // 检查 PATH 环境变量
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator))
        {
            var npnpPath = Path.Combine(dir, "npnp.exe");
            if (File.Exists(npnpPath))
            {
                _logMessages.Add($"在 PATH 中找到 npnp: {npnpPath}");
                return npnpPath;
            }
            npnpPath = Path.Combine(dir, "npnp");
            if (File.Exists(npnpPath))
            {
                _logMessages.Add($"在 PATH 中找到 npnp: {npnpPath}");
                return npnpPath;
            }
        }

        _logMessages.Add("未找到 npnp 可执行文件");
        return null;
    }

    /// <summary>
    /// 搜索元件
    /// </summary>
    public async Task<List<SearchItem>> SearchAsync(string keyword, int limit = 10)
    {
        if (!IsAvailable)
        {
            _logMessages.Add("npnp 不可用，无法搜索");
            return new List<SearchItem>();
        }

        var args = $"search {keyword} --limit {limit}";
        var output = await RunCommandAsync(args);
        
        return ParseSearchOutput(output, keyword);
    }

    /// <summary>
    /// 导出原理图库
    /// </summary>
    public async Task<bool> ExportSchLibAsync(string lcscId, string outputDir, string? libraryName = null)
    {
        if (!IsAvailable)
        {
            _logMessages.Add("npnp 不可用，无法导出 SchLib");
            return false;
        }

        var args = $"export-schlib {lcscId}";
        if (!string.IsNullOrEmpty(libraryName))
        {
            args += $" --library-name {libraryName}";
        }
        args += $" --output {outputDir} --force";

        var output = await RunCommandAsync(args);
        _logMessages.Add($"SchLib 导出输出: {output}");
        
        return output.Contains("success") || output.Contains("complete");
    }

    /// <summary>
    /// 导出 PCB 库
    /// </summary>
    public async Task<bool> ExportPcbLibAsync(string lcscId, string outputDir, string? libraryName = null)
    {
        if (!IsAvailable)
        {
            _logMessages.Add("npnp 不可用，无法导出 PcbLib");
            return false;
        }

        var args = $"export-pcblib {lcscId}";
        if (!string.IsNullOrEmpty(libraryName))
        {
            args += $" --library-name {libraryName}";
        }
        args += $" --output {outputDir} --force";

        var output = await RunCommandAsync(args);
        _logMessages.Add($"PcbLib 导出输出: {output}");
        
        return output.Contains("success") || output.Contains("complete");
    }

    /// <summary>
    /// 批量导出
    /// </summary>
    public async Task<bool> BatchExportAsync(IEnumerable<string> lcscIds, string outputDir, bool merge = true, string? libraryName = null)
    {
        if (!IsAvailable)
        {
            _logMessages.Add("npnp 不可用，无法批量导出");
            return false;
        }

        // 创建临时文件存储 LCSC ID
        var tempFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(tempFile, lcscIds);

            var args = $"batch --input {tempFile} --output {outputDir}";
            if (merge)
            {
                args += " --merge";
            }
            if (!string.IsNullOrEmpty(libraryName))
            {
                args += $" --library-name {libraryName}";
            }
            args += " --full --force --continue-on-error";

            var output = await RunCommandAsync(args);
            _logMessages.Add($"批量导出输出: {output}");
            
            return output.Contains("success") || output.Contains("complete");
        }
        finally
        {
            try { File.Delete(tempFile); } catch { }
        }
    }

    private async Task<string> RunCommandAsync(string arguments)
    {
        if (string.IsNullOrEmpty(_npnpPath))
        {
            return string.Empty;
        }

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _npnpPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                }
            };

            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            process.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    outputBuilder.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (s, e) =>
            {
                if (e.Data != null)
                {
                    errorBuilder.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync();

            var output = outputBuilder.ToString();
            var error = errorBuilder.ToString();

            if (!string.IsNullOrEmpty(error))
            {
                _logMessages.Add($"npnp 错误输出: {error}");
            }

            return output;
        }
        catch (Exception ex)
        {
            _logMessages.Add($"执行 npnp 失败: {ex.Message}");
            return string.Empty;
        }
    }

    private List<SearchItem> ParseSearchOutput(string output, string keyword)
    {
        var items = new List<SearchItem>();
        
        if (string.IsNullOrEmpty(output))
        {
            return items;
        }

        // 解析 npnp 搜索输出
        // 输出格式大概是:
        // 1. [C12345] Component Name - Manufacturer
        var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        
        int index = 1;
        foreach (var line in lines)
        {
            if (line.Trim().StartsWith("["))
            {
                var item = ParseSearchLine(line, index);
                if (item != null)
                {
                    items.Add(item);
                    index++;
                }
            }
        }

        return items;
    }

    private SearchItem? ParseSearchLine(string line, int index)
    {
        try
        {
            // 格式: 1. [C12345] Component Name - Manufacturer
            var match = System.Text.RegularExpressions.Regex.Match(line, @"\[(C\d+)\]\s*(.+?)(?:\s*-\s*(.+))?$");
            if (match.Success)
            {
                var lcscId = match.Groups[1].Value;
                var name = match.Groups[2].Value.Trim();
                var manufacturer = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";

                return new SearchItem(lcscId, name, "", manufacturer);
            }
        }
        catch
        {
            // 解析失败，忽略
        }

        return null;
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Npnp.Core.Services;

/// <summary>
/// npnp CLI 工具调用实现
/// </summary>
public class NpnpCliService : INpnpCliService
{
    private readonly string? _executablePath;

    public NpnpCliService()
    {
        // 1. 优先从应用程序根目录的 tools 子目录查找
        var baseDir = AppContext.BaseDirectory;
        var toolsPath = Path.Combine(baseDir, "tools", "npnp.exe");
        if (File.Exists(toolsPath))
        {
            _executablePath = toolsPath;
            return;
        }

        // 2. 尝试从程序集所在目录的 tools 子目录查找
        var assemblyLocation = Assembly.GetExecutingAssembly().Location;
        if (!string.IsNullOrEmpty(assemblyLocation))
        {
            var assemblyDir = Path.GetDirectoryName(assemblyLocation);
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                var altPath = Path.Combine(assemblyDir, "tools", "npnp.exe");
                if (File.Exists(altPath))
                {
                    _executablePath = altPath;
                    return;
                }
            }
        }

        // 3. 尝试在系统 PATH 中查找
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir, "npnp.exe");
                    if (File.Exists(candidate))
                    {
                        _executablePath = candidate;
                        return;
                    }
                }
                catch
                {
                    // 忽略无效路径
                }
            }
        }

        _executablePath = null;
    }

    public bool IsAvailable() => !string.IsNullOrEmpty(_executablePath) && File.Exists(_executablePath);

    public string GetExecutablePath() => _executablePath ?? throw new InvalidOperationException("npnp.exe 未找到");

    public NpnpExportResult BatchExport(
        IEnumerable<string> lcscIds,
        string outputDir,
        bool mergeOutput = false,
        string? libraryName = null,
        bool embedStepModel = false)
    {
        var result = new NpnpExportResult();

        if (!IsAvailable())
        {
            result.ErrorMessage = "npnp.exe 未找到。请确保 tools/npnp.exe 存在。";
            return result;
        }

        var ids = lcscIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            result.ErrorMessage = "没有要导出的元件 ID。";
            return result;
        }

        // 准备临时 ID 文件
        var tempIdFile = Path.Combine(Path.GetTempPath(), $"npnp_ids_{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllLines(tempIdFile, ids, Encoding.ASCII);

            // 准备输出目录
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 构建命令参数
            var args = new List<string>
            {
                "batch",
                "--input", $"\"{tempIdFile}\"",
                "--output", $"\"{outputDir}\"",
                "--force",
                "--continue-on-error"
            };

            if (embedStepModel)
            {
                args.Add("--full");
            }

            if (mergeOutput && !string.IsNullOrWhiteSpace(libraryName))
            {
                args.Add("--merge");
                args.Add("--library-name");
                args.Add($"\"{libraryName}\"");
                args.Add("--full");
            }

            var arguments = string.Join(" ", args);

            // 执行命令
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = startInfo };
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();

            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data != null) stdout.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            result.ExitCode = process.ExitCode;
            result.Output = stdout.ToString() + stderr.ToString();
            result.Success = process.ExitCode == 0;

            if (!result.Success)
            {
                result.ErrorMessage = $"npnp 退出码 {process.ExitCode}。输出: {result.Output}";
            }
            else
            {
                // 收集生成的文件
                CollectGeneratedFiles(outputDir, result.GeneratedFiles);
            }
        }
        catch (Exception ex)
        {
            result.ErrorMessage = $"调用 npnp 时出错: {ex.Message}";
            result.Success = false;
        }
        finally
        {
            try
            {
                if (File.Exists(tempIdFile))
                {
                    File.Delete(tempIdFile);
                }
            }
            catch
            {
                // 忽略
            }
        }

        return result;
    }

    private void CollectGeneratedFiles(string outputDir, List<string> files)
    {
        if (!Directory.Exists(outputDir)) return;

        // 收集 schlib 目录下的所有 .SchLib 文件
        var schlibDir = Path.Combine(outputDir, "schlib");
        if (Directory.Exists(schlibDir))
        {
            files.AddRange(Directory.GetFiles(schlibDir, "*.SchLib", SearchOption.AllDirectories));
        }

        // 收集 pcblib 目录下的所有 .PcbLib 文件
        var pcblibDir = Path.Combine(outputDir, "pcblib");
        if (Directory.Exists(pcblibDir))
        {
            files.AddRange(Directory.GetFiles(pcblibDir, "*.PcbLib", SearchOption.AllDirectories));
        }

        // 也搜索输出根目录
        files.AddRange(Directory.GetFiles(outputDir, "*.SchLib", SearchOption.TopDirectoryOnly));
        files.AddRange(Directory.GetFiles(outputDir, "*.PcbLib", SearchOption.TopDirectoryOnly));
    }
}

using Npnp.Core.Models;
using Npnp.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class BatchCommand : AsyncCommand<BatchSettings>
{
    private readonly IExportService _exportService;
    private readonly ILcscApiService _lcscApiService;

    public BatchCommand(IExportService exportService, ILcscApiService lcscApiService)
    {
        _exportService = exportService;
        _lcscApiService = lcscApiService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, BatchSettings settings)
    {
        var lines = await File.ReadAllLinesAsync(settings.Input);
        var lcscIds = lines
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lcscIds.Count == 0)
        {
            AnsiConsole.MarkupLine("[red]输入文件中没有有效的 LCSC ID[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[cyan]正在批量导出 {lcscIds.Count} 个元件...[/]");

        var outputDir = Path.GetFullPath(settings.Output);
        Directory.CreateDirectory(outputDir);

        var libraryName = settings.LibraryName ?? "NpnpLibrary";
        var options = new ExportOptions(
            LibraryName: libraryName,
            EmbedStepModel: settings.EmbedStep,
            ForceOverwrite: settings.Force,
            MergeOutput: settings.Merge,
            AppendMode: settings.Append);

        var total = lcscIds.Count;
        var completed = 0;
        var failed = 0;

        await AnsiConsole.Progress()
            .AutoRefresh(true)
            .AutoClear(false)
            .HideCompleted(false)
            .Columns(new ProgressColumn[]
            {
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new RemainingTimeColumn(),
                new SpinnerColumn()
            })
            .StartAsync(async ctx =>
            {
                var progressTask = ctx.AddTask("[cyan]导出进度[/]", maxValue: total);

                var components = new List<ComponentDetail>();

                foreach (var lcscId in lcscIds)
                {
                    progressTask.Description = $"[cyan]正在处理: {lcscId}[/]";

                    try
                    {
                        var component = await _lcscApiService.GetComponentAsync(lcscId);
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
                        if (settings.ContinueOnError)
                        {
                            failed++;
                        }
                        else
                        {
                            throw;
                        }
                    }

                    progressTask.Increment(1);
                }

                progressTask.Description = "[cyan]正在写入文件...[/]";

                if (components.Count > 0)
                {
                    var schPath = Path.Combine(outputDir, $"{libraryName}.SchLib");
                    var pcbPath = Path.Combine(outputDir, $"{libraryName}.PcbLib");

                    AnsiConsole.MarkupLine($"[green]成功: {completed}, 失败: {failed}[/]");
                    AnsiConsole.MarkupLine($"[green]SchLib 导出到: {schPath}[/]");
                    AnsiConsole.MarkupLine($"[green]PcbLib 导出到: {pcbPath}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine("[yellow]没有元件可以导出[/]");
                }

                progressTask.Value = progressTask.MaxValue;
            });

        return failed > 0 && !settings.ContinueOnError ? 1 : 0;
    }
}

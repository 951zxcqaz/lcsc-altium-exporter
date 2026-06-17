using Npnp.Core.Models;
using Npnp.Core.Services;
using Npnp.Core.Writers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class ExportCommand : AsyncCommand<ExportSettings>
{
    private readonly ILcscApiService _lcscApiService;
    private readonly ISchLibWriter _schLibWriter;
    private readonly IPcbLibWriter _pcbLibWriter;

    public ExportCommand(ILcscApiService lcscApiService, ISchLibWriter schLibWriter, IPcbLibWriter pcbLibWriter)
    {
        _lcscApiService = lcscApiService;
        _schLibWriter = schLibWriter;
        _pcbLibWriter = pcbLibWriter;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, ExportSettings settings)
    {
        AnsiConsole.MarkupLine($"[cyan]正在获取元件详情: {settings.LcscId}...[/]");

        var component = await _lcscApiService.GetComponentAsync(settings.LcscId);
        if (component == null)
        {
            AnsiConsole.MarkupLine($"[red]未找到元件: {settings.LcscId}[/]");
            return 1;
        }

        var exportType = settings.Type.ToLowerInvariant();
        if (exportType != "sch" && exportType != "pcb" && exportType != "both")
        {
            AnsiConsole.MarkupLine($"[red]不支持的导出类型: {exportType}，请使用 sch、pcb 或 both[/]");
            return 1;
        }

        var outputDir = Path.GetFullPath(settings.Output);
        Directory.CreateDirectory(outputDir);

        var libraryName = settings.LibraryName ?? component.Name;
        var options = new ExportOptions(
            LibraryName: libraryName,
            EmbedStepModel: settings.EmbedStep,
            ForceOverwrite: settings.Force);

        var components = new[] { component };

        if (exportType == "sch" || exportType == "both")
        {
            if (component.Symbol == null)
            {
                AnsiConsole.MarkupLine($"[yellow]该元件没有原理图符号，跳过 SchLib 导出[/]");
            }
            else
            {
                var schPath = Path.Combine(outputDir, $"{libraryName}.SchLib");
                _schLibWriter.Write(schPath, components, options);
                AnsiConsole.MarkupLine($"[green]原理图库已导出到: {schPath}[/]");
            }
        }

        if (exportType == "pcb" || exportType == "both")
        {
            if (component.Footprint == null)
            {
                AnsiConsole.MarkupLine($"[yellow]该元件没有 PCB 封装，跳过 PcbLib 导出[/]");
            }
            else
            {
                var pcbPath = Path.Combine(outputDir, $"{libraryName}.PcbLib");
                _pcbLibWriter.Write(pcbPath, components, options);
                AnsiConsole.MarkupLine($"[green]PCB 库已导出到: {pcbPath}[/]");
            }
        }

        return 0;
    }
}

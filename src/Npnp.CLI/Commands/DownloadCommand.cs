using Npnp.Core.Models;
using Npnp.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class DownloadCommand : AsyncCommand<DownloadSettings>
{
    private readonly ILcscApiService _lcscApiService;

    public DownloadCommand(ILcscApiService lcscApiService)
    {
        _lcscApiService = lcscApiService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, DownloadSettings settings)
    {
        AnsiConsole.MarkupLine($"[cyan]正在搜索元件 {settings.LcscId}...[/]");

        var searchResult = await _lcscApiService.SearchAsync(settings.LcscId, 1);
        var item = searchResult.Items.FirstOrDefault();

        if (item == null)
        {
            AnsiConsole.MarkupLine($"[red]未找到元件: {settings.LcscId}[/]");
            return 1;
        }

        var component = await _lcscApiService.GetComponentAsync(settings.LcscId);
        if (component == null)
        {
            AnsiConsole.MarkupLine($"[red]无法获取元件详情: {settings.LcscId}[/]");
            return 1;
        }

        var format = settings.Format.ToLowerInvariant();
        if (format != "step" && format != "obj")
        {
            AnsiConsole.MarkupLine($"[red]不支持的格式: {format}，请使用 step 或 obj[/]");
            return 1;
        }

        AnsiConsole.MarkupLine($"[cyan]正在下载 {format.ToUpper()} 模型...[/]");

        byte[]? modelData = null;
        string extension;

        if (format == "step")
        {
            if (string.IsNullOrEmpty(component.StepModel?.StepUrl))
            {
                AnsiConsole.MarkupLine($"[yellow]该元件没有 STEP 模型[/]");
                return 1;
            }
            modelData = await _lcscApiService.DownloadStepModelAsync(component.StepModel.StepUrl);
            extension = "step";
        }
        else
        {
            if (string.IsNullOrEmpty(component.StepModel?.ObjUrl))
            {
                AnsiConsole.MarkupLine($"[yellow]该元件没有 OBJ 模型[/]");
                return 1;
            }
            modelData = await _lcscApiService.DownloadObjModelAsync(component.StepModel.ObjUrl);
            extension = "obj";
        }

        if (modelData == null || modelData.Length == 0)
        {
            AnsiConsole.MarkupLine($"[red]模型下载失败[/]");
            return 1;
        }

        var outputDir = Path.GetFullPath(settings.Output);
        Directory.CreateDirectory(outputDir);

        var fileName = $"{component.LcscId}_{component.Name}.{extension}";
        fileName = string.Join("_", fileName.Split(Path.GetInvalidFileNameChars()));
        var outputPath = Path.Combine(outputDir, fileName);

        await File.WriteAllBytesAsync(outputPath, modelData);

        AnsiConsole.MarkupLine($"[green]模型已保存到: {outputPath}[/]");
        return 0;
    }
}

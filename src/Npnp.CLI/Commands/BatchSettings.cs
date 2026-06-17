using System.ComponentModel;
using Spectre.Console.Cli;
using Spectre.Console;

namespace Npnp.CLI.Commands;

public class BatchSettings : CommandSettings
{
    [CommandOption("-i|--input")]
    public string Input { get; set; } = string.Empty;

    [CommandOption("-o|--output")]
    [DefaultValue(".")]
    public string Output { get; set; } = ".";

    [CommandOption("-t|--type")]
    [DefaultValue("both")]
    public string Type { get; set; } = "both";

    [CommandOption("-n|--name")]
    public string? LibraryName { get; set; }

    [CommandOption("--merge")]
    public bool Merge { get; set; }

    [CommandOption("--append")]
    public bool Append { get; set; }

    [CommandOption("--step")]
    public bool EmbedStep { get; set; }

    [CommandOption("--force")]
    public bool Force { get; set; }

    [CommandOption("--continue")]
    public bool ContinueOnError { get; set; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(Input))
        {
            return ValidationResult.Error("必须指定输入文件 (--input/-i)");
        }

        var exportType = Type?.ToLowerInvariant();
        if (exportType != "sch" && exportType != "pcb" && exportType != "both")
        {
            return ValidationResult.Error($"不支持的导出类型: {exportType}，请使用 sch、pcb 或 both");
        }

        return ValidationResult.Success();
    }
}

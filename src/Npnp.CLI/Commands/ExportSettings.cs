using System.ComponentModel;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class ExportSettings : CommandSettings
{
    [CommandArgument(0, "<lcsc-id>")]
    public string LcscId { get; set; } = string.Empty;

    [CommandOption("-t|--type")]
    [DefaultValue("both")]
    public string Type { get; set; } = "both";

    [CommandOption("-o|--output")]
    [DefaultValue(".")]
    public string Output { get; set; } = ".";

    [CommandOption("-n|--name")]
    public string? LibraryName { get; set; }

    [CommandOption("--step")]
    public bool EmbedStep { get; set; }

    [CommandOption("--force")]
    public bool Force { get; set; }
}

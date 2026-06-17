using System.ComponentModel;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class DownloadSettings : CommandSettings
{
    [CommandArgument(0, "<lcsc-id>")]
    public string LcscId { get; set; } = string.Empty;

    [CommandOption("-f|--format")]
    [DefaultValue("step")]
    public string Format { get; set; } = "step";

    [CommandOption("-o|--output")]
    [DefaultValue(".")]
    public string Output { get; set; } = ".";
}

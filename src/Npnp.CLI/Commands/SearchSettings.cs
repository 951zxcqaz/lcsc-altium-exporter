using System.ComponentModel;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class SearchSettings : CommandSettings
{
    [CommandArgument(0, "<keyword>")]
    public string Keyword { get; set; } = string.Empty;

    [CommandOption("-l|--limit")]
    [DefaultValue(10)]
    public int Limit { get; set; } = 10;

    [CommandOption("-j|--json")]
    public bool Json { get; set; }
}

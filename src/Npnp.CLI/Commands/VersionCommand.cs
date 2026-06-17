using Spectre.Console;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class VersionCommand : Command
{
    public override int Execute(CommandContext context)
    {
        AnsiConsole.MarkupLine("[bold]npnp 1.0.0[/bold]");
        return 0;
    }
}

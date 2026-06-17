using Npnp.Core.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Npnp.CLI.Commands;

public class SearchCommand : AsyncCommand<SearchSettings>
{
    private readonly ILcscApiService _lcscApiService;

    public SearchCommand(ILcscApiService lcscApiService)
    {
        _lcscApiService = lcscApiService;
    }

    public override async Task<int> ExecuteAsync(CommandContext context, SearchSettings settings)
    {
        var result = await _lcscApiService.SearchAsync(settings.Keyword, settings.Limit);

        if (settings.Json)
        {
            System.Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result));
            return 0;
        }

        var table = new Spectre.Console.Table();
        table.AddColumn("LCSC ID");
        table.AddColumn("名称");
        table.AddColumn("描述");

        foreach (var item in result.Items)
        {
            table.AddRow(item.LcscId, item.Name, item.Description);
        }

        AnsiConsole.Write(table);
        return 0;
    }
}

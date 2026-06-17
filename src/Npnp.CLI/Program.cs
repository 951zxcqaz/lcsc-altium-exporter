using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;
using Npnp.Core;
using Npnp.CLI.Commands;

namespace Npnp.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        // 版本信息
        const string Version = "1.0.0";
        const string Description = "Normalize Pin Net Pad - LCEDA Downloader and Altium Library Exporter";

        if (args.Length == 0 || args[0] == "--version" || args[0] == "-v")
        {
            Console.WriteLine($"npnp {Version}");
            Console.WriteLine(Description);
            return 0;
        }

        if (args[0] == "--help" || args[0] == "-h")
        {
            Console.WriteLine($"npnp {Version}");
            Console.WriteLine(Description);
            Console.WriteLine();
            Console.WriteLine("Usage: npnp [command] [options]");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  search        Search components by keyword");
            Console.WriteLine("  download      Download 3D models");
            Console.WriteLine("  export        Export Altium libraries");
            Console.WriteLine("  batch         Batch export from file");
            Console.WriteLine("  version       Show version information");
            Console.WriteLine("  help          Show this help message");
            return 0;
        }

        try
        {
            // 调用 Spectre.Console.Cli
            var app = new CommandApp(new TypeRegistrar());
            
            app.Configure(config =>
            {
                config.AddCommand<SearchCommand>("search");
                config.AddCommand<DownloadCommand>("download");
                config.AddCommand<ExportCommand>("export");
                config.AddCommand<BatchCommand>("batch");
                config.AddCommand<VersionCommand>("version");
            });
            
            return await app.RunAsync(args);
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            return 1;
        }
    }
}

public class TypeRegistrar : ITypeRegistrar
{
    private readonly IServiceProvider _provider;

    public TypeRegistrar()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _provider = services.BuildServiceProvider();
    }

    public ITypeResolver Build() => new TypeResolver(_provider);

    public void Register(Type serviceType, Type implementationType)
    {
        // DI registration handled in ConfigureServices
    }

    public void RegisterInstance(Type serviceType, object implementation)
    {
        // DI registration handled in ConfigureServices
    }

    public void RegisterLazy(Type serviceType, Func<object> factory)
    {
        // DI registration handled in ConfigureServices
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddNpnpCore();
    }
}

public class TypeResolver : ITypeResolver
{
    private readonly IServiceProvider _provider;

    public TypeResolver(IServiceProvider provider)
    {
        _provider = provider;
    }

    public object Resolve(Type type)
    {
        return _provider.GetService(type)!;
    }
}

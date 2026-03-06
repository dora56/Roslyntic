using Roslyntic.Cli.Commands;
using Roslyntic.Cli.Logging;

var logger = new StderrLogger();

if (args.Length < 2 || args[0] != "check")
{
    Console.Error.WriteLine("Usage: roslyntic check <path-to-sln-or-csproj>");
    return 2;
}

var path = args[1];
return await CheckCommand.RunAsync(path, logger, CancellationToken.None);

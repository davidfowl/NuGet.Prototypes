using System;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime.Common.CommandLine;

namespace NuGet3
{
    public class Program
    {
        public int Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "nuget3";

            var optionVerbose = app.Option("-v|--verbose", "Show verbose output", CommandOptionType.NoValue);
            app.HelpOption("-?|-h|--help");
            app.VersionOption("--version", GetVersion());

            // Show help information if no subcommand/option was specified
            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 2;
            });

            app.Command("pack", c =>
            {
                c.Description = "Creates a nuget package from the specified project.json";
                var projectArg = c.Argument("[root]", "Path to a project.json");

                c.OnExecute(() =>
                {
                    var command = new PackCommand();
                    command.ProjectFile = projectArg.Value ?? Directory.GetCurrentDirectory();
                    return command.Execute() ? 0 : 1;
                });
            });

            app.Command("dump", c =>
            {
                c.Description = "Copies relevant dependencies to target folder";
                var projectArg = c.Argument("[root]", "Path to a project.json");

                c.OnExecute(() =>
                {
                    var command = new DumpCommand();
                    command.Logger = new AnsiConsoleLogger(false, false);
                    command.ProjectFile = projectArg.Value ?? Directory.GetCurrentDirectory();

                    return command.Execute() ? 0 : 1;
                });
            });

            app.Command("restore", c =>
            {
                c.Description = "Restore packages";

                var argRoot = c.Argument("[root]", "Root of all projects to restore. It can be a directory, a project.json, or a global.json.");
                var optSource = c.Option("-s|--source <FEED>", "A list of packages sources to use for this command",
                    CommandOptionType.MultipleValue);
                var optFallbackSource = c.Option("-f|--fallbacksource <FEED>",
                    "A list of packages sources to use as a fallback", CommandOptionType.MultipleValue);
                var optProxy = c.Option("-p|--proxy <ADDRESS>", "The HTTP proxy to use when retrieving packages",
                    CommandOptionType.SingleValue);
                var optNoCache = c.Option("--no-cache", "Do not use local cache", CommandOptionType.NoValue);
                var optPackageFolder = c.Option("--packages", "Path to restore packages", CommandOptionType.SingleValue);
                var optQuiet = c.Option("--quiet", "Do not show output such as HTTP request/cache information",
                    CommandOptionType.NoValue);
                var optIgnoreFailedSources = c.Option("--ignore-failed-sources",
                    "Ignore failed remote sources if there are local packages meeting version requirements",
                    CommandOptionType.NoValue);
                c.HelpOption("-?|-h|--help");

                c.OnExecute(async () =>
                {
                    var command = new RestoreCommand();
                    command.Logger = new AnsiConsoleLogger(optionVerbose.HasValue(), optQuiet.HasValue());

                    command.RestoreDirectory = argRoot.Value;
                    command.Sources = optSource.Values;
                    command.FallbackSources = optFallbackSource.Values;
                    command.NoCache = optNoCache.HasValue();
                    command.PackageFolder = optPackageFolder.Value();
                    command.IgnoreFailedSources = optIgnoreFailedSources.HasValue();

                    if (optProxy.HasValue())
                    {
                        Environment.SetEnvironmentVariable("http_proxy", optProxy.Value());
                    }

                    var success = await command.ExecuteCommand();

                    return success ? 0 : 1;
                });
            });

            return app.Execute(args);
        }
        
        private static string GetVersion()
        {
            var assembly = typeof(Program).GetTypeInfo().Assembly;
            var assemblyInformationalVersionAttribute = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            return assemblyInformationalVersionAttribute.InformationalVersion;
        }
    }
}

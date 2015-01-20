
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet;
using NuGet.Common;
using NuGet.Configuration;
using NuGet.DependencyResolver;
using NuGet.Packaging.Extensions;

namespace NuGet3
{
    public class RestoreCommand
    {
        public RestoreCommand()
        {
            MachineWideSettings = new CommandLineMachineWideSettings();
            Sources = Enumerable.Empty<string>();
            FallbackSources = Enumerable.Empty<string>();
        }

        public string RestoreDirectory { get; set; }

        public IEnumerable<string> Sources { get; set; }
        public IEnumerable<string> FallbackSources { get; set; }
        public bool NoCache { get; set; }
        public string PackageFolder { get; set; }
        public bool IgnoreFailedSources { get; set; }

        // public ScriptExecutor ScriptExecutor { get; private set; }
        // public string NuGetConfigFile { get; set; }
        // public IApplicationEnvironment ApplicationEnvironment { get; private set; }

        public IMachineWideSettings MachineWideSettings { get; set; }
        public Reports Reports { get; set; }

        protected internal ISettings Settings { get; set; }
        protected internal IPackageSourceProvider SourceProvider { get; set; }

        public async Task<bool> ExecuteCommand()
        {
            try
            {
                var sw = Stopwatch.StartNew();

                // If the root argument is a project.json file
                if (string.Equals(
                    Project.ProjectFileName,
                    Path.GetFileName(RestoreDirectory),
                    StringComparison.OrdinalIgnoreCase))
                {
                    RestoreDirectory = Path.GetDirectoryName(Path.GetFullPath(RestoreDirectory));
                }
                if (!Directory.Exists(RestoreDirectory) && !string.IsNullOrEmpty(RestoreDirectory))
                {
                    throw new InvalidOperationException("The given root is invalid.");
                }

                var restoreDirectory = RestoreDirectory ?? Directory.GetCurrentDirectory();

                var rootDirectory = ProjectResolver.ResolveRootDirectory(restoreDirectory);
                ReadSettings(rootDirectory);

                string packagesDirectory = PackageFolder;

                if (string.IsNullOrEmpty(PackageFolder))
                {
                    packagesDirectory = ResolveRestoreTarget(rootDirectory);
                }

                int restoreCount = 0;
                int successCount = 0;

                var projectJsonFiles = Directory.GetFiles(restoreDirectory, "project.json", SearchOption.AllDirectories);
                foreach (var projectJsonPath in projectJsonFiles)
                {
                    restoreCount += 1;
                    var success = await RestoreForProject(projectJsonPath, rootDirectory, packagesDirectory);
                    if (success)
                    {
                        successCount += 1;
                    }
                }

                if (restoreCount > 1)
                {
                    Reports.WriteInformation(string.Format("Total time {0}ms", sw.ElapsedMilliseconds));
                }

                return restoreCount == successCount;
            }
            catch (Exception ex)
            {
                Reports.WriteInformation("----------");
                Reports.WriteInformation(ex.ToString());
                Reports.WriteInformation("----------");
                Reports.WriteInformation("Restore failed");
                Reports.WriteInformation(ex.Message);
                return false;
            }
        }

        private string ResolveRestoreTarget(string rootDirectory)
        {
            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            // TODO: Change this
            return Path.Combine(profileDirectory, ".kpm", "packages");
        }

        private async Task<bool> RestoreForProject(string projectJsonPath, string rootDirectory, string packagesDirectory)
        {
            var success = true;

            Reports.WriteInformation(string.Format("Restoring packages for {0}", projectJsonPath.Bold()));

            var sw = new Stopwatch();
            sw.Start();

            Project project;
            if (!Project.TryGetProject(projectJsonPath, out project))
            {
                throw new Exception("Unable to locate project.json");
            }

            var projectDirectory = project.ProjectDirectory;
            var projectProviders = new List<IRemoteDependencyProvider>();
            var localProviders = new List<IRemoteDependencyProvider>();
            var remoteProviders = new List<IRemoteDependencyProvider>();
            var contexts = new List<RemoteWalkContext>();

            projectProviders.Add(
                new LocalDependencyProvider(
                    new ProjectReferenceDependencyProvider(
                        new ProjectResolver(
                            projectDirectory,
                            rootDirectory))));

            localProviders.Add(
                new LocalDependencyProvider(
                    new NuGetDependencyResolver(
                        packagesDirectory)));

            var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(SourceProvider,
                Sources, FallbackSources);

            AddRemoteProvidersFromSources(remoteProviders, effectiveSources);
            
            //if (!contexts.Any())
            //{
            //    contexts.Add(new RestoreContext
            //    {
            //        FrameworkName = ApplicationEnvironment.RuntimeFramework,
            //        ProjectLibraryProviders = projectProviders,
            //        LocalLibraryProviders = localProviders,
            //        RemoteLibraryProviders = remoteProviders,
            //    });
            //}

            var tasks = new List<Task<GraphNode<RemoteResolveResult>>>();

            foreach (var framework in project.GetTargetFrameworks())
            {
                var context = new RemoteWalkContext
                {
                    ProjectLibraryProviders = projectProviders,
                    LocalLibraryProviders = localProviders,
                    RemoteLibraryProviders = remoteProviders,
                };

                // This is so that we have a unique cache per target framework
                var remoteWalker = new RemoteDependencyWalker(context);
                tasks.Add(remoteWalker.Walk(project.Name, project.Version, framework.FrameworkName));
            }

            var graphs = await Task.WhenAll(tasks);

            Reports.WriteInformation(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(), sw.ElapsedMilliseconds));

            var installItems = new List<GraphItem<RemoteResolveResult>>();
            var missingItems = new HashSet<LibraryRange>();

            foreach (var g in graphs)
            {
                g.Dump(s => Reports.WriteInformation(s));
                g.TryResolveConflicts();
                g.Dump(s => Reports.WriteInformation(s));
            }

            //foreach (var g in graphs)
            //{
            //    g.TryResolveConflicts();
            //}

            // TODO: Do stuff here
            //foreach (var item in graphs)
            //{
            //    installItems.AddRange(item.InstallItems);
            //    foreach (var missing in item.MissingItems)
            //    {
            //        Reports.WriteError(string.Format("Unable to locate {0} {1}", missing.Name.Red().Bold(), missing.VersionRange));
            //        success = false;
            //        missingItems.Add(missing);
            //    }
            //}

            //ForEach(graphs, node =>
            //{
            //    if (node == null || node.LibraryRange == null)
            //    {
            //        return;
            //    }

            //    if (node.Item == null || node.Item.Match == null)
            //    {
            //        if (!node.LibraryRange.IsGacOrFrameworkReference &&
            //             node.LibraryRange.VersionRange != null &&
            //             missingItems.Add(node.LibraryRange))
            //        {
            //            Reports.WriteError(string.Format("Unable to locate {0} {1}", node.LibraryRange.Name.Red().Bold(), node.LibraryRange.VersionRange));
            //            success = false;
            //        }

            //        return;
            //    }

            //    var isRemote = remoteProviders.Contains(node.Item.Match.Provider);
            //    var isAdded = installItems.Any(item => item.Match.Library == node.Item.Match.Library);

            //    if (!isAdded && isRemote)
            //    {
            //        installItems.Add(node.Item);
            //    }
            //});

            await InstallPackages(installItems, packagesDirectory, packageFilter: (library, nupkgSHA) => true);

            //if (!ScriptExecutor.Execute(project, "postrestore", getVariable))
            //{
            //    Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
            //    return false;
            //}

            //if (!ScriptExecutor.Execute(project, "prepare", getVariable))
            //{
            //    Reports.Error.WriteLine(ScriptExecutor.ErrorMessage);
            //    return false;
            //}

            Reports.WriteInformation(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

            //for (int i = 0; i < contexts.Count; i++)
            //{
            //    PrintDependencyGraph(graphs[i], contexts[i].FrameworkName);
            //}

            return success;
        }

        //private async Task<bool> RestoreFromGlobalJson(string rootDirectory, string packagesDirectory)
        //{
        //    var success = true;

        //    Reports.Information.WriteLine(string.Format("Restoring packages for {0}", Path.GetFullPath(GlobalJsonFile).Bold()));

        //    var sw = new Stopwatch();
        //    sw.Start();

        //    var restoreOperations = new RestoreOperations(Reports.Information);
        //    var localProviders = new List<IWalkProvider>();
        //    var remoteProviders = new List<IWalkProvider>();

        //    localProviders.Add(
        //        new LocalWalkProvider(
        //            new NuGetDependencyResolver(
        //                packagesDirectory)));

        //    var effectiveSources = PackageSourceUtils.GetEffectivePackageSources(SourceProvider,
        //        Sources, FallbackSources);

        //    AddRemoteProvidersFromSources(remoteProviders, effectiveSources);

        //    var context = new RestoreContext
        //    {
        //        FrameworkName = ApplicationEnvironment.RuntimeFramework,
        //        ProjectLibraryProviders = new List<IWalkProvider>(),
        //        LocalLibraryProviders = localProviders,
        //        RemoteLibraryProviders = remoteProviders,
        //    };

        //    GlobalSettings globalSettings;
        //    GlobalSettings.TryGetGlobalSettings(GlobalJsonFile, out globalSettings);

        //    var libsToRestore = globalSettings.PackageHashes.Keys.ToList();

        //    var tasks = new List<Task<GraphItem>>();

        //    foreach (var library in libsToRestore)
        //    {
        //        tasks.Add(restoreOperations.FindLibraryCached(context, library));
        //    }

        //    var resolvedItems = await Task.WhenAll(tasks);

        //    Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Resolving complete".Green(),
        //        sw.ElapsedMilliseconds));

        //    var installItems = new List<GraphItem>();
        //    var missingItems = new List<Library>();

        //    for (int i = 0; i < resolvedItems.Length; i++)
        //    {
        //        var item = resolvedItems[i];
        //        var library = libsToRestore[i];

        //        if (item == null || 
        //            item.Match == null || 
        //            item.Match.Library.Version != library.Version)
        //        {
        //            missingItems.Add(library);

        //            Reports.Error.WriteLine(string.Format("Unable to locate {0} {1}",
        //                library.Name.Red().Bold(), library.Version));

        //            success = false;
        //            continue;
        //        }

        //        var isRemote = remoteProviders.Contains(item.Match.Provider);
        //        var isAdded = installItems.Any(x => x.Match.Library == item.Match.Library);

        //        if (!isAdded && isRemote)
        //        {
        //            installItems.Add(item);
        //        }
        //    }

        //    await InstallPackages(installItems, packagesDirectory, packageFilter: (library, nupkgSHA) =>
        //    {
        //        string expectedSHA = globalSettings.PackageHashes[library];

        //        if (!string.Equals(expectedSHA, nupkgSHA, StringComparison.Ordinal))
        //        {
        //            Reports.Error.WriteLine(
        //                string.Format("SHA of downloaded package {0} doesn't match expected value.".Red().Bold(),
        //                library.ToString()));

        //            success = false;
        //            return false;
        //        }

        //        return true;
        //    });

        //    Reports.Information.WriteLine(string.Format("{0}, {1}ms elapsed", "Restore complete".Green().Bold(), sw.ElapsedMilliseconds));

        //    return success;
        //}

        private async Task InstallPackages(List<GraphItem<RemoteResolveResult>> installItems, string packagesDirectory,
            Func<Library, string, bool> packageFilter)
        {
            using (var sha512 = SHA512.Create())
            {
                foreach (var item in installItems)
                {
                    var library = item.Data.Match.Library;

                    var memStream = new MemoryStream();
                    await item.Data.Match.Provider.CopyToAsync(item.Data.Match, memStream);
                    memStream.Seek(0, SeekOrigin.Begin);
                    var nupkgSHA = Convert.ToBase64String(sha512.ComputeHash(memStream));

                    bool shouldInstall = packageFilter(library, nupkgSHA);
                    if (!shouldInstall)
                    {
                        continue;
                    }

                    Reports.WriteInformation(string.Format("Installing {0} {1}", library.Name.Bold(), library.Version));
                    memStream.Seek(0, SeekOrigin.Begin);
                    await NuGetPackageUtils.InstallFromStream(memStream, library, packagesDirectory, sha512);
                }
            }
        }

        private void AddRemoteProvidersFromSources(List<IRemoteDependencyProvider> remoteProviders, List<PackageSource> effectiveSources)
        {
            foreach (var source in effectiveSources)
            {
                var feed = PackageSourceUtils.CreatePackageFeed(source, NoCache, IgnoreFailedSources, Reports);
                if (feed != null)
                {
                    remoteProviders.Add(new RemoteDependencyProvider(feed));
                }
            }
        }

        //private void PrintDependencyGraph(GraphNode root, FrameworkName frameworkName)
        //{
        //    // Box Drawing Unicode characters:
        //    // http://www.unicode.org/charts/PDF/U2500.pdf
        //    const char LIGHT_HORIZONTAL = '\u2500';
        //    const char LIGHT_UP_AND_RIGHT = '\u2514';
        //    const char LIGHT_VERTICAL_AND_RIGHT = '\u251C';

        //    var frameworkSuffix = string.Format(" [{0}]", frameworkName.ToString());
        //    Reports.Verbose.WriteLine(root.Item.Match.Library.ToString() + frameworkSuffix);

        //    Func<GraphNode, bool> isValidDependency = d =>
        //        (d != null && d.LibraryRange != null && d.Item != null && d.Item.Match != null);
        //    var dependencies = root.Dependencies.Where(isValidDependency).ToList();
        //    var dependencyNum = dependencies.Count;
        //    for (int i = 0; i < dependencyNum; i++)
        //    {
        //        var branchChar = LIGHT_VERTICAL_AND_RIGHT;
        //        if (i == dependencyNum - 1)
        //        {
        //            branchChar = LIGHT_UP_AND_RIGHT;
        //        }

        //        var name = dependencies[i].Item.Match.Library.ToString();
        //        var dependencyListStr = string.Join(", ", dependencies[i].Dependencies
        //            .Where(isValidDependency)
        //            .Select(d => d.Item.Match.Library.ToString()));
        //        var format = string.IsNullOrEmpty(dependencyListStr) ? "{0}{1} {2}{3}" : "{0}{1} {2} ({3})";
        //        Reports.Verbose.WriteLine(string.Format(format,
        //            branchChar, LIGHT_HORIZONTAL, name, dependencyListStr));
        //    }
        //    Reports.Verbose.WriteLine();
        //}

        //private static void ExtractPackage(string targetPath, FileStream stream)
        //{
        //    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
        //    {
        //        var packOperations = new PackOperations();
        //        packOperations.ExtractNupkg(archive, targetPath);
        //    }
        //}

        //void ForEach(IEnumerable<GraphNode> nodes, Action<GraphNode> callback)
        //{
        //    foreach (var node in nodes)
        //    {
        //        callback(node);
        //        ForEach(node.Dependencies, callback);
        //    }
        //}

        //void Display(string indent, IEnumerable<GraphNode> graphs)
        //{
        //    foreach (var node in graphs)
        //    {
        //        Reports.Information.WriteLine(indent + node.Item.Match.Library.Name + "@" + node.Item.Match.Library.Version);
        //        Display(indent + " ", node.Dependencies);
        //    }
        //}


        private void ReadSettings(string path)
        {
            Settings = NuGet.Configuration.Settings.LoadDefaultSettings(path, configFileName: null, machineWideSettings: MachineWideSettings);

            // Recreate the source provider and credential provider
            SourceProvider = PackageSourceBuilder.CreateSourceProvider(Settings);

            //HttpClient.DefaultCredentialProvider = new SettingsCredentialProvider(new ConsoleCredentialProvider(Console), SourceProvider, Console);

        }
    }
}
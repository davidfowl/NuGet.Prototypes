using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.Client;
using NuGet.Common;
using NuGet.ContentModel;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.MSBuild;
using NuGet.ProjectModel;
using NuGet.Repositories;
using NuGet.Versioning;
using NuGetProject = NuGet.ProjectModel.Project;

namespace NuGet3
{
    public class DumpCommand
    {
        public DumpCommand()
        {
            Patterns = new PatternDefinitions();
        }

        public ILogger Logger { get; set; }

        public string ProjectFile { get; set; }

        public string PatternName { get; set; }

        public PatternDefinitions Patterns { get; set; }

        public bool Execute()
        {
            var providers = new List<IDependencyProvider>();

            var packagesPath = GetPackagesPath();
            var projectDirectory = Path.GetDirectoryName(ProjectFile);

            var projectCollection = new ProjectCollection();
            var projectResolver = new ProjectResolver(projectDirectory);

            string name;
            NuGetFramework targetFramework;
            NuGetVersion version;

            GetProjectInfo(projectResolver,
                           projectCollection, out name, out targetFramework, out version);

            if (projectCollection.Count > 0)
            {
                // Handle MSBuild projects
                providers.Add(new MSBuildDependencyProvider(projectCollection));
            }
            else
            {
                providers.Add(new ProjectReferenceDependencyProvider(projectResolver));
            }

            var lockFilePath = Path.Combine(projectDirectory, LockFileFormat.LockFileName);

            if (File.Exists(lockFilePath))
            {
                var lockFileFormat = new LockFileFormat();
                var lockFile = lockFileFormat.Read(lockFilePath);

                // Handle dependencies from the lock file
                providers.Add(new LockFileDependencyProvider(lockFile));
            }
            else
            {
                // Use the global packages folder if available
                providers.Add(new NuGetDependencyResolver(packagesPath));
            }

            var walker = new DependencyWalker(providers);

            var searchCriteria = GetSelectionCriteria(targetFramework);

            // This is so that we have a unique cache per target framework
            var root = walker.Walk(name, version, targetFramework);

            Logger.WriteInformation("Unresolved closure".Yellow());

            // Raw graph
            root.Dump(Logger.WriteInformation);

            // Cousin resolution
            root.TryResolveConflicts();

            Logger.WriteInformation("Unified closure".Yellow());

            // Dump dependency graph after resolution
            root.Dump(Logger.WriteInformation);

            var resolvedItems = new Dictionary<string, GraphItem<ResolveResult>>();

            // Pick the relevant versions of the package after conflict
            // resolution
            root.ForEach(true, (node, state) =>
            {
                if (state == false ||
                    node.Disposition != Disposition.Accepted ||
                    node.Item == null)
                {
                    return false;
                }

                if (!resolvedItems.ContainsKey(node.Key.Name))
                {
                    resolvedItems[node.Key.Name] = node.Item;
                }

                return true;
            });

            var seen = new HashSet<string>();
            var queue = new Queue<ResolveResult>();

            // The reason we don't just look the packages as a flat list is because we'll need to 
            // eventually handle private dependencies which require knowledge of the graph
            queue.Enqueue(resolvedItems[name].Data);

            while (queue.Count > 0)
            {
                var top = queue.Dequeue();

                if (!seen.Add(top.LibraryDescription.Identity.Name))
                {
                    continue;
                }

                DumpPackageContents(top.LibraryDescription, searchCriteria);

                foreach (var dependency in top.LibraryDescription.Dependencies)
                {
                    GraphItem<ResolveResult> dependencyItem;
                    if (resolvedItems.TryGetValue(dependency.Name, out dependencyItem))
                    {
                        queue.Enqueue(dependencyItem.Data);
                    }
                }
            }

            return true;
        }

        private void GetProjectInfo(ProjectResolver projectResolver,
                                    ProjectCollection projectCollection,
                                    out string name,
                                    out NuGetFramework targetFramework,
                                    out NuGetVersion version)
        {

            // Project

            name = new DirectoryInfo(Path.GetDirectoryName(ProjectFile)).Name;
            NuGetProject nugetProject;

            if (projectResolver.TryResolveProject(name, out nugetProject))
            {
                targetFramework = nugetProject.TargetFrameworks.LastOrDefault()?.FrameworkName;
                version = nugetProject.Version;
                return;
            }

            // MSBuild project

            var project = projectCollection.LoadProject(ProjectFile);
            name = project.FullPath;
            targetFramework = NuGetFramework.Parse(project.GetPropertyValue("TargetFrameworkMoniker"));
            version = new NuGetVersion(new Version());
        }

        private SelectionCriteria GetSelectionCriteria(NuGetFramework projectFramework)
        {
            // This API isn't great but it allows you the client to build up search criteria
            // based on patterns inside of the package

            var criteria = new SelectionCriteria();
            var entry = new SelectionCriteriaEntry();

            entry.Properties["tfm"] = projectFramework;
            entry.Properties["tpm"] = null;

            criteria.Entries.Add(entry);

            return criteria;
        }

        private void DumpPackageContents(LibraryDescription library, SelectionCriteria criteria)
        {
            var packageContents = new ContentItemCollection();
            var files = library.GetItem<IEnumerable<string>>("files");

            if (files != null)
            {
                packageContents.Load(files);
            }
            else
            {
                var packageInfo = library.GetItem<LocalPackageInfo>("package");

                if (packageInfo == null)
                {
                    return;
                }

                packageContents.Load(packageInfo.ManifestPath);
            }


            var group = packageContents.FindBestItemGroup(criteria, Patterns.CompileTimeAssemblies, Patterns.ManagedAssemblies);

            if (group == null)
            {
                // No matching groups
                return;
            }

            Logger.WriteInformation(library.ToString().White());
            Logger.WriteInformation("=========================");
            foreach (var item in group.Items)
            {
                Logger.WriteInformation(item.Path.White());

                foreach (var property in item.Properties)
                {
                    Logger.WriteInformation(property.Key.Yellow() + " = " + property.Value);
                }
            }

            Logger.WriteInformation("=========================");
            Console.WriteLine();
        }

        private string GetPackagesPath()
        {
            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            // TODO: Change this
            return Path.Combine(profileDirectory, ".k", "packages");
        }
    }
}
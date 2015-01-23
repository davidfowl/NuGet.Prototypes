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
using NuGet.MSBuild;
using NuGet.Packaging.Extensions;
using NuGet.Versioning;

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
            var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(ProjectFile);

            var providers = new List<IDependencyProvider>();

            var packagesPath = GetPackagesPath();

            // Handle MSBuild projects
            providers.Add(new MSBuildDependencyProvider(projectCollection));

            // Handle NuGet dependencies
            providers.Add(new NuGetDependencyResolver(packagesPath));

            var walker = new DependencyWalker(providers);

            var name = project.GetPropertyValue("AssemblyName");
            var targetFramework = NuGetFramework.Parse(project.GetPropertyValue("TargetFrameworkMoniker"));
            var version = new NuGetVersion(new Version());

            var searchCriteria = GetPropertyDefinition(targetFramework);

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

                if (top.DependencyProvider is NuGetDependencyResolver)
                {
                    DumpPackageContents(top.LibraryDescription, searchCriteria);
                }

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

        private SelectionCriteria GetPropertyDefinition(NuGetFramework projectFramework)
        {
            // This API isn't great but it allows you the client to build up search criteria
            // based on patterns inside of the package

            // TODO: Handle tpms

            return new SelectionCriteriaBuilder(Patterns.Properties.Definitions)
                .Add["tfm", projectFramework]["tpm", null]
                .Criteria;
        }

        private void DumpPackageContents(LibraryDescription library, SelectionCriteria criteria)
        {
            var packageContents = new ContentItemCollection();
            packageContents.Load(Path.GetDirectoryName(library.Path));

            var group = packageContents.FindBestItemGroup(criteria, Patterns.ManagedAssemblies);

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
            return Path.Combine(profileDirectory, ".dotnet", "packages");
        }
    }
}
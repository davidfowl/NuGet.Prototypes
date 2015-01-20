// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NuGet.Frameworks;
using NuGet.Packaging.Extensions;
using NuGet.Versioning;
using NuGet.Versioning.Extensions;

namespace NuGet.Resolver
{
    public class RemoteDependencyWalker
    {
        public async Task<RemoteResolveResults> Walk(RemoteWalkContext context, string name, NuGetVersion version)
        {
            var root = await CreateGraphNode(context, new LibraryRange
            {
                Name = name,
                VersionRange = new NuGetVersionRange(version)
            });

            var results = new RemoteResolveResults();

            ForEach(new[] { root }, node =>
             {
                 if (node == null || node.LibraryRange == null)
                 {
                     return;
                 }

                 if (node.LibraryRange.IsGacOrFrameworkReference)
                 {
                     return;
                 }

                 if (node.Item == null || node.Item.Match == null)
                 {
                     results.MissingItems.Add(node.LibraryRange);
                     return;
                 }

                 var isRemote = context.RemoteLibraryProviders.Contains(node.Item.Match.Provider);
                 var isAdded = results.InstallItems.Any(item => item.Match.Library == node.Item.Match.Library);

                 if (!isAdded && isRemote)
                 {
                     results.InstallItems.Add(node.Item);
                 }
             });

            return results;
        }

        private void ForEach(IEnumerable<GraphNode> nodes, Action<GraphNode> callback)
        {
            foreach (var node in nodes)
            {
                callback(node);
                ForEach(node.Dependencies, callback);
            }
        }

        private Task<GraphNode> CreateGraphNode(RemoteWalkContext context, LibraryRange libraryRange)
        {
            return CreateGraphNode(context, libraryRange, _ => true);
        }

        private async Task<GraphNode> CreateGraphNode(RemoteWalkContext context, LibraryRange libraryRange, Func<string, bool> predicate)
        {
            var node = new GraphNode
            {
                LibraryRange = libraryRange,
                Item = await FindLibraryCached(context, libraryRange),
            };

            if (node.Item != null)
            {
                if (node.LibraryRange.VersionRange != null &&
                    node.LibraryRange.VersionRange.VersionFloatBehavior != NuGetVersionFloatBehavior.None)
                {
                    lock (context.FindLibraryCache)
                    {
                        if (!context.FindLibraryCache.ContainsKey(node.LibraryRange))
                        {
                            context.FindLibraryCache[node.LibraryRange] = Task.FromResult(node.Item);
                        }
                    }
                }

                var tasks = new List<Task<GraphNode>>();
                var dependencies = node.Item.Dependencies ?? Enumerable.Empty<LibraryDependency>();
                foreach (var dependency in dependencies)
                {
                    if (predicate(dependency.Name))
                    {
                        tasks.Add(CreateGraphNode(context, dependency.LibraryRange, ChainPredicate(predicate, node.Item, dependency)));
                    }
                }

                while (tasks.Any())
                {
                    var task = await Task.WhenAny(tasks);
                    tasks.Remove(task);
                    var dependency = await task;
                    node.Dependencies.Add(dependency);
                }
            }

            return node;
        }

        private Func<string, bool> ChainPredicate(Func<string, bool> predicate, GraphItem item, LibraryDependency dependency)
        {
            return name =>
            {
                if (item.Match.Library.Name == name)
                {
                    throw new Exception(string.Format("Circular dependency references not supported. Package '{0}'.", name));
                }

                if (item.Dependencies.Any(d => d != dependency && d.Name == name))
                {
                    return false;
                }

                return predicate(name);
            };
        }

        public Task<GraphItem> FindLibraryCached(RemoteWalkContext context, LibraryRange libraryRange)
        {
            lock (context.FindLibraryCache)
            {
                Task<GraphItem> task;
                if (!context.FindLibraryCache.TryGetValue(libraryRange, out task))
                {
                    task = FindLibraryEntry(context, libraryRange);
                    context.FindLibraryCache[libraryRange] = task;
                }

                return task;
            }
        }

        private async Task<GraphItem> FindLibraryEntry(RemoteWalkContext context, LibraryRange libraryRange)
        {
            var match = await FindLibraryMatch(context, libraryRange);

            if (match == null)
            {
                return null;
            }

            var dependencies = await match.Provider.GetDependencies(match, context.FrameworkName);

            return new GraphItem
            {
                Match = match,
                Dependencies = dependencies,
            };
        }

        private async Task<RemoteResolveResult> FindLibraryMatch(RemoteWalkContext context, LibraryRange libraryRange)
        {
            var projectMatch = await FindProjectMatch(context, libraryRange.Name);

            if (projectMatch != null)
            {
                return projectMatch;
            }

            if (libraryRange.VersionRange == null)
            {
                return null;
            }

            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            if (libraryRange.VersionRange.VersionFloatBehavior != NuGetVersionFloatBehavior.None)
            {
                // For snapshot dependencies, get the version remotely first.
                var remoteMatch = await FindLibraryByVersion(context, libraryRange, context.RemoteLibraryProviders);
                if (remoteMatch == null)
                {
                    // If there was nothing remotely, use the local match (if any)
                    var localMatch = await FindLibraryByVersion(context, libraryRange, context.LocalLibraryProviders);
                    return localMatch;
                }
                else
                {
                    // Try to see if the specific version found on the remote exists locally. This avoids any unnecessary
                    // remote access incase we already have it in the cache/local packages folder.
                    var localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);

                    if (localMatch != null && localMatch.Library.Version.Equals(remoteMatch.Library.Version))
                    {
                        // If we have a local match, and it matches the version *exactly* then use it.
                        return localMatch;
                    }

                    // We found something locally, but it wasn't an exact match
                    // for the resolved remote match.
                    return remoteMatch;
                }
            }
            else
            {
                // Check for the specific version locally.
                var localMatch = await FindLibraryByVersion(context, libraryRange, context.LocalLibraryProviders);

                if (localMatch != null && localMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
                {
                    // We have an exact match so use it.
                    return localMatch;
                }

                // Either we found a local match but it wasn't the exact version, or 
                // we didn't find a local match.
                var remoteMatch = await FindLibraryByVersion(context, libraryRange, context.RemoteLibraryProviders);

                if (remoteMatch != null && localMatch == null)
                {
                    // There wasn't any local match for the specified version but there was a remote match.
                    // See if that version exists locally.
                    localMatch = await FindLibraryByVersion(context, remoteMatch.Library, context.LocalLibraryProviders);
                }

                if (localMatch != null && remoteMatch != null)
                {
                    // We found a match locally and remotely, so pick the better version
                    // in relation to the specified version.
                    if (libraryRange.VersionRange.IsBetter(
                        current: localMatch.Library.Version,
                        considering: remoteMatch.Library.Version))
                    {
                        return remoteMatch;
                    }
                    else
                    {
                        return localMatch;
                    }
                }

                // Prefer local over remote generally.
                return localMatch ?? remoteMatch;
            }
        }

        private async Task<RemoteResolveResult> FindProjectMatch(RemoteWalkContext context, string name)
        {
            var libraryRange = new LibraryRange
            {
                Name = name
            };

            foreach (var provider in context.ProjectLibraryProviders)
            {
                var match = await provider.FindLibrary(libraryRange, context.FrameworkName);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private async Task<RemoteResolveResult> FindLibraryByVersion(RemoteWalkContext context, LibraryRange libraryRange, IEnumerable<IWalkProvider> providers)
        {
            if (libraryRange.VersionRange.VersionFloatBehavior != NuGetVersionFloatBehavior.None)
            {
                // Don't optimize the non http path for floating versions or we'll miss things
                return await FindLibrary(libraryRange, providers, provider => provider.FindLibrary(libraryRange, context.FrameworkName));
            }

            // Try the non http sources first
            var nonHttpMatch = await FindLibrary(libraryRange, providers.Where(p => !p.IsHttp), provider => provider.FindLibrary(libraryRange, context.FrameworkName));

            // If we found an exact match then use it
            if (nonHttpMatch != null && nonHttpMatch.Library.Version.Equals(libraryRange.VersionRange.MinVersion))
            {
                return nonHttpMatch;
            }

            // Otherwise try the http sources
            var httpMatch = await FindLibrary(libraryRange, providers.Where(p => p.IsHttp), provider => provider.FindLibrary(libraryRange, context.FrameworkName));

            // Pick the best match of the 2
            if (libraryRange.VersionRange.IsBetter(
                nonHttpMatch?.Library?.Version,
                httpMatch?.Library.Version))
            {
                return httpMatch;
            }

            return nonHttpMatch;
        }

        private static async Task<RemoteResolveResult> FindLibrary(
            LibraryRange libraryRange,
            IEnumerable<IWalkProvider> providers,
            Func<IWalkProvider, Task<RemoteResolveResult>> action)
        {
            var tasks = new List<Task<RemoteResolveResult>>();
            foreach (var provider in providers)
            {
                tasks.Add(action(provider));
            }

            RemoteResolveResult bestMatch = null;
            var matches = await Task.WhenAll(tasks);
            foreach (var match in matches)
            {
                if (libraryRange.VersionRange.IsBetter(
                    current: bestMatch?.Library?.Version,
                    considering: match?.Library?.Version))
                {
                    bestMatch = match;
                }
            }

            return bestMatch;
        }
    }
}
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Extensions;
using NuGet.Versioning;
using NuGet.Versioning.Extensions;

namespace NuGet.DependencyResolver
{
    public class DependencyWalker
    {
        private readonly IEnumerable<IDependencyProvider> _dependencyProviders;

        public DependencyWalker(IEnumerable<IDependencyProvider> dependencyProviders)
        {
            _dependencyProviders = dependencyProviders;
        }

        public GraphNode<ResolveResult> Walk(string name, NuGetVersion version, NuGetFramework framework)
        {
            var root = new GraphNode<ResolveResult>
            {
                Key = new LibraryRange
                {
                    Name = name,
                    VersionRange = new NuGetVersionRange(version)
                }
            };

            var resolvedItems = new Dictionary<LibraryRange, GraphItem<ResolveResult>>();

            // Recurse through dependencies optimistically, asking resolvers for dependencies
            // based on best match of each encountered dependency
            root.ForEach(node =>
            {
                node.Item = Resolve(resolvedItems, node.Key, framework);
                if (node.Item == null)
                {
                    node.Disposition = Disposition.Rejected;
                    return;
                }

                foreach (var dependency in node.Item.Data.LibraryDescription.Dependencies)
                {
                    // determine if a child dependency is eclipsed by
                    // a reference on the line leading to this point. this
                    // prevents cyclical dependencies, and also implements the
                    // "nearest wins" rule.

                    var eclipsed = false;
                    for (var scanNode = node;
                         scanNode != null && !eclipsed;
                         scanNode = scanNode.OuterNode)
                    {
                        eclipsed |= string.Equals(
                            scanNode.Key.Name,
                            dependency.Name,
                            StringComparison.OrdinalIgnoreCase);

                        if (eclipsed)
                        {
                            break;
                        }

                        foreach (var sideNode in scanNode.InnerNodes)
                        {
                            eclipsed |= string.Equals(
                                sideNode.Key.Name,
                                dependency.Name,
                                StringComparison.OrdinalIgnoreCase);

                            if (eclipsed)
                            {
                                break;
                            }
                        }
                    }

                    if (!eclipsed)
                    {
                        var innerNode = new GraphNode<ResolveResult>
                        {
                            OuterNode = node,
                            Key = dependency.LibraryRange
                        };

                        node.InnerNodes.Add(innerNode);
                    }
                }
            });

            return root;
        }

        private GraphItem<ResolveResult> Resolve(
            Dictionary<LibraryRange, GraphItem<ResolveResult>> resolvedItems,
            LibraryRange packageKey,
            NuGetFramework framework)
        {
            GraphItem<ResolveResult> item;
            if (resolvedItems.TryGetValue(packageKey, out item))
            {
                return item;
            }

            Tuple<IDependencyProvider, LibraryDescription> hit = null;

            foreach (var dependencyProvider in _dependencyProviders)
            {
                var match = dependencyProvider.GetDescription(packageKey, framework);
                if (match != null)
                {
                    hit = Tuple.Create(dependencyProvider, match);
                    break;
                }
            }

            if (hit == null)
            {
                resolvedItems[packageKey] = null;
                return null;
            }

            var provider = hit.Item1;
            var libraryDescripton = hit.Item2;

            if (resolvedItems.TryGetValue(libraryDescripton.Identity, out item))
            {
                return item;
            }

            item = new GraphItem<ResolveResult>()
            {
                Key = libraryDescripton.Identity,
                Data = new ResolveResult
                {
                    LibraryDescription = libraryDescripton,
                    DependencyProvider = provider
                }
            };

            resolvedItems[packageKey] = item;
            resolvedItems[libraryDescripton.Identity] = item;
            return item;
        }
    }
}

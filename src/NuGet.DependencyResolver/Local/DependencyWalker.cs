// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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
            var key = new LibraryRange
            {
                Name = name,
                VersionRange = new NuGetVersionRange(version)
            };

            var root = new GraphNode<ResolveResult>(key);

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
                        var innerNode = new GraphNode<ResolveResult>(dependency.LibraryRange)
                        {
                            OuterNode = node
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

            ResolveResult hit = null;

            foreach (var dependencyProvider in _dependencyProviders)
            {
                var match = dependencyProvider.GetDescription(packageKey, framework);
                if (match != null)
                {
                    hit = new ResolveResult
                    {
                        DependencyProvider = dependencyProvider,
                        LibraryDescription = match
                    };

                    break;
                }
            }

            if (hit == null)
            {
                resolvedItems[packageKey] = null;
                return null;
            }

            if (resolvedItems.TryGetValue(hit.LibraryDescription.Identity, out item))
            {
                return item;
            }

            item = new GraphItem<ResolveResult>(hit.LibraryDescription.Identity)
            {
                Data = hit
            };

            resolvedItems[packageKey] = item;
            resolvedItems[hit.LibraryDescription.Identity] = item;
            return item;
        }
    }
}

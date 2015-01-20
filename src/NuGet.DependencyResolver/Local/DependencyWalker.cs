// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Extensions;
using NuGet.Versioning;

namespace NuGet.Resolver
{
    public class DependencyWalker
    {
        private readonly IEnumerable<IDependencyProvider> _dependencyProviders;
        
        public DependencyWalker(IEnumerable<IDependencyProvider> dependencyProviders)
        {
            _dependencyProviders = dependencyProviders;
        }

        public IEnumerable<ResolveResult> Walk(string name, NuGetVersion version, NuGetFramework targetFramework)
        {
            var context = new WalkContext();
            
            context.Walk(
                _dependencyProviders,
                name,
                version,
                targetFramework);

            return context.GetResolvedItems();
           
        }
    }
}

// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using NuGet.Packaging.Extensions;
using NuGet.Frameworks;

namespace NuGet.DependencyResolver
{
    public class RemoteWalkContext
    {
        public RemoteWalkContext()
        {
            FindLibraryCache = new Dictionary<LibraryRange, Task<RemoteGraphItem>>();
        }

        public NuGetFramework FrameworkName { get; set; }

        public IList<IWalkProvider> ProjectLibraryProviders { get; set; }
        public IList<IWalkProvider> LocalLibraryProviders { get; set; }
        public IList<IWalkProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<RemoteGraphItem>> FindLibraryCache { get; private set; }
    }
}

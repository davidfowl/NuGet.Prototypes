// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading.Tasks;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public class RemoteWalkContext
    {
        public RemoteWalkContext()
        {
            FindLibraryCache = new Dictionary<LibraryRange, Task<GraphItem<RemoteResolveResult>>>();
        }

        public IList<IRemoteDependencyProvider> ProjectLibraryProviders { get; set; }
        public IList<IRemoteDependencyProvider> LocalLibraryProviders { get; set; }
        public IList<IRemoteDependencyProvider> RemoteLibraryProviders { get; set; }

        public Dictionary<LibraryRange, Task<GraphItem<RemoteResolveResult>>> FindLibraryCache { get; private set; }
    }
}

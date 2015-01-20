// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.Resolver
{
    public class GraphItem
    {
        public RemoteResolveResult Match { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
    }
}

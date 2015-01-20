// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public class RemoteGraphNode
    {
        public RemoteGraphNode()
        {
            Dependencies = new List<RemoteGraphNode>();
        }

        public LibraryRange LibraryRange { get; set; }
        public List<RemoteGraphNode> Dependencies { get; private set; }
        public RemoteGraphItem Item { get; set; }
    }

}
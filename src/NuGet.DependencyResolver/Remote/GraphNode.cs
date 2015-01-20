// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.Resolver
{
    public class GraphNode
    {
        public GraphNode()
        {
            Dependencies = new List<GraphNode>();
        }

        public LibraryRange LibraryRange { get; set; }
        public List<GraphNode> Dependencies { get; private set; }
        public GraphItem Item { get; set; }
    }

}
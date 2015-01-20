using System;
using System.Collections.Generic;
using System.Diagnostics;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    [DebuggerDisplay("{Key}")]
    public class GraphItem
    {
        public LibraryDescription Description { get; set; }
        public Library Key { get; set; }
        public IDependencyProvider Resolver { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
    }

}
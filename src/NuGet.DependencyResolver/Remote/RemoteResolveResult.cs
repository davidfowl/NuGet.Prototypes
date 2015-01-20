using System;
using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public class RemoteResolveResult
    {
        public RemoteMatch Match { get; set; }
        public IEnumerable<LibraryDependency> Dependencies { get; set; }
    }
}
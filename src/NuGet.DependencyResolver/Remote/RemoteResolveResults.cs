using System;
using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.Resolver
{
    public class RemoteResolveResults
    {
        public List<GraphItem> InstallItems { get; }
        public HashSet<LibraryRange> MissingItems { get; }

        public RemoteResolveResults()
        {
            InstallItems = new List<GraphItem>();
            MissingItems = new HashSet<LibraryRange>();
        }
    }
}
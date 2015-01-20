using System;
using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public class RemoteResolveResults
    {
        public List<RemoteGraphItem> InstallItems { get; }
        public HashSet<LibraryRange> MissingItems { get; }

        public RemoteResolveResults()
        {
            InstallItems = new List<RemoteGraphItem>();
            MissingItems = new HashSet<LibraryRange>();
        }
    }
}
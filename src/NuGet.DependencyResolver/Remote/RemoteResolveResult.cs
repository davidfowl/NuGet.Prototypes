using System;
using NuGet.Packaging.Extensions;

namespace NuGet.Resolver
{
    public class RemoteResolveResult
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

}
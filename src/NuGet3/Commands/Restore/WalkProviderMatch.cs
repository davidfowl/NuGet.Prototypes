using System;
using NuGet.Packaging.Extensions;

namespace NuGet3
{
    public class WalkProviderMatch
    {
        public IWalkProvider Provider { get; set; }
        public Library Library { get; set; }
        public string Path { get; set; }
    }

}
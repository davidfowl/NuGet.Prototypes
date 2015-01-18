using System;
using System.IO;
using NuGet.Versioning;

namespace NuGet.Repositories
{
    public class LocalPackageInfo
    {
        public LocalPackageInfo(string packageId, NuGetVersion version, string path)
        {
            Id = packageId;
            Version = version;
            ManifestPath = Path.Combine(path, string.Format("{0}.nuspec", Id));
            ZipPath = Path.Combine(path, string.Format("{0}.{1}.nupkg", Id, Version));
        }

        public string Id { get; }

        public NuGetVersion Version { get; }

        public string ManifestPath { get; }

        public string ZipPath { get; }
    }
}
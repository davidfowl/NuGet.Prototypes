using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Repositories;

namespace NuGet.Client
{
    public class NuGetv2LocalRepository
    {
        private readonly string _physicalPath;

        public NuGetv2LocalRepository(string physicalPath)
        {
            _physicalPath = physicalPath;
        }

        public IEnumerable<LocalPackageInfo> FindPackagesById(string id)
        {
            var packages = Directory.EnumerateFiles(_physicalPath, id + "*.nupkg");

            foreach (var file in packages)
            {
                using (var stream = File.OpenRead(file))
                {
                    var zip = new ZipArchive(stream);
                    var spec = zip.GetManifest();

                    using (var specStream = spec.Open())
                    {
                        var reader = new NuspecReader(specStream);
                        if (string.Equals(reader.GetId(), id, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new LocalPackageInfo(reader.GetId(), reader.GetVersion(), _physicalPath);
                        }
                    }
                }
            }
        }
    }
}
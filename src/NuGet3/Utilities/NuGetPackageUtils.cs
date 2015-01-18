// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Extensions;

namespace NuGet3
{
    internal static class NuGetPackageUtils
    {
        internal static async Task InstallFromStream(Stream stream, Library library, string packagesDirectory,
            SHA512 sha512)
        {
            var packagePathResolver = new DefaultPackagePathResolver(packagesDirectory);

            var targetPath = packagePathResolver.GetInstallPath(library.Name, library.Version);
            var targetNuspec = packagePathResolver.GetManifestFilePath(library.Name, library.Version);
            var targetNupkg = packagePathResolver.GetPackageFilePath(library.Name, library.Version);
            var hashPath = packagePathResolver.GetHashPath(library.Name, library.Version);

            // Acquire the lock on a nukpg before we extract it to prevent the race condition when multiple
            // processes are extracting to the same destination simultaneously
            await ConcurrencyUtilities.ExecuteWithFileLocked(targetNupkg, async createdNewLock =>
            {
                // If this is the first process trying to install the target nupkg, go ahead
                // After this process successfully installs the package, all other processes
                // waiting on this lock don't need to install it again
                if (createdNewLock)
                {
                    Directory.CreateDirectory(targetPath);
                    using (var nupkgStream = new FileStream(targetNupkg, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite | FileShare.Delete))
                    {
                        await stream.CopyToAsync(nupkgStream);
                        nupkgStream.Seek(0, SeekOrigin.Begin);

                        ExtractPackage(targetPath, nupkgStream);
                    }

                    //// Fixup the casing of the nuspec on disk to match what we expect
                    //var nuspecFile = Directory.EnumerateFiles(targetPath, "*" + Constants.ManifestExtension).Single();

                    //if (!string.Equals(nuspecFile, targetNuspec, StringComparison.Ordinal))
                    //{
                    //    Manifest manifest = null;
                    //    using (var nuspecStream = File.OpenRead(nuspecFile))
                    //    {
                    //        manifest = Manifest.ReadFrom(nuspecStream, validateSchema: false);
                    //        manifest.Metadata.Id = library.Name;
                    //    }

                    //    // Delete the previous nuspec file
                    //    File.Delete(nuspecFile);

                    //    // Write the new manifest
                    //    using (var targetNuspecStream = File.OpenWrite(targetNuspec))
                    //    {
                    //        manifest.Save(targetNuspecStream);
                    //    }
                    //}

                    stream.Seek(0, SeekOrigin.Begin);
                    var nupkgSHA = Convert.ToBase64String(sha512.ComputeHash(stream));
                    File.WriteAllText(hashPath, nupkgSHA);
                }

                return 0;
            });
        }

        private static void ExtractPackage(string targetPath, FileStream stream)
        {
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                archive.ExtractNupkg(targetPath);
            }
        }
    }
}
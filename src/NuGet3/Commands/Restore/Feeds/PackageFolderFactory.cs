// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet3;

namespace NuGet3
{
    internal static class PackageFolderFactory
    {
        public static IPackageFeed CreatePackageFolderFromPath(string path, bool ignoreFailedSources, Reports reports)
        {
            Func<string, bool> containsNupkg = dir => Directory.Exists(dir) &&
                Directory.EnumerateFiles(dir, "*.nupkg")
                .Where(x => !Path.GetFileNameWithoutExtension(x).EndsWith(".symbols"))
                .Any();

            if (Directory.Exists(path) &&
                (containsNupkg(path) || Directory.EnumerateDirectories(path).Any(x => containsNupkg(x))))
            {
                return new NuGetv2PackageFolder(path, reports);
            }
            else
            {
                return new NuGetv3PackageFolder(path, ignoreFailedSources, reports);
            }
        }
    }
}
// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Extensions;
using NuGet.Packaging.Extensions.Frameworks;
using NuGet.Repositories;
using NuGet.Versioning.Extensions;

namespace NuGet.DependencyResolver
{
    public class NuGetDependencyResolver : IDependencyProvider
    {
        private readonly NuGetv3LocalRepository _repository;

        public NuGetDependencyResolver(NuGetv3LocalRepository repository)
        {
            _repository = repository;
        }

        public NuGetDependencyResolver(string packagesPath)
        {
            _repository = new NuGetv3LocalRepository(packagesPath, checkPackageIdCase: false);
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            var package = FindCandidate(libraryRange.Name, libraryRange.VersionRange);

            if (package != null)
            {
                return new LibraryDescription
                {
                    LibraryRange = libraryRange,
                    Identity = new Library
                    {
                        Name = package.Id,
                        Version = package.Version
                    },
                    Path = package.ManifestPath,
                    Dependencies = GetDependencies(package, targetFramework)
                };
            }

            return null;
        }

        private IEnumerable<LibraryDependency> GetDependencies(LocalPackageInfo package, NuGetFramework targetFramework)
        {
            NuspecReader nuspecReader = null;
            using (var stream = File.OpenRead(package.ManifestPath))
            {
                nuspecReader = new NuspecReader(stream);
            }

            var reducer = new FrameworkReducer();

            var deps = nuspecReader.GetDependencyGroups()
                                   .ToDictionary(g => new NuGetFramework(g.TargetFramework),
                                                 g => g.Packages);


            var nearest = reducer.GetNearest(targetFramework, deps.Keys);

            if (nearest != null)
            {
                foreach (var d in deps[nearest])
                {
                    yield return new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = d.Id,
                            VersionRange = d.VersionRange == null ? null : new NuGetVersionRange(d.VersionRange)
                        }
                    };
                }
            }

            // TODO: Remove this when we do #596
            // ASP.NET Core isn't compatible with generic PCL profiles
            //if (string.Equals(targetFramework.Identifier, VersionUtility.AspNetCoreFrameworkIdentifier, StringComparison.OrdinalIgnoreCase))
            //{
            //    yield break;
            //}

            var frameworks = nuspecReader.GetFrameworkReferenceGroups()
                                         .ToDictionary(f => f.TargetFramework,
                                                       f => f.Items);

            nearest = reducer.GetNearest(targetFramework, frameworks.Keys) ?? frameworks.Keys.FirstOrDefault(f => f.AnyPlatform);

            if (nearest != null)
            {
                if (nearest.AnyPlatform && !targetFramework.IsDesktop())
                {
                    // REVIEW: This isn't 100% correct since none *can* mean 
                    // any in theory, but in practice it means .NET full reference assembly
                    // If there's no supported target frameworks and we're not targeting
                    // the desktop framework then skip it.

                    // To do this properly we'll need all reference assemblies supported
                    // by each supported target framework which isn't always available.
                    yield break;
                }

                foreach (var name in frameworks[nearest])
                {
                    yield return new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = name,
                            IsGacOrFrameworkReference = true
                        }
                    };
                }
            }
        }

        private LocalPackageInfo FindCandidate(string name, NuGetVersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            if (versionRange == null)
            {
                // TODO: Disallow null versions for nuget packages
                var packageInfo = packages.FirstOrDefault();
                if (packageInfo != null)
                {
                    return packageInfo;
                }

                return null;
            }

            LocalPackageInfo bestMatch = null;

            foreach (var packageInfo in packages)
            {
                if (versionRange.IsBetter(
                    current: bestMatch?.Version,
                    considering: packageInfo.Version))
                {
                    bestMatch = packageInfo;
                }
            }

            if (bestMatch == null)
            {
                return null;
            }

            return bestMatch;
        }
    }
}

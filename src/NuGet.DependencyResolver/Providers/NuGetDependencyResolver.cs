// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;
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

            var dependencies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetDependencyGroups(),
                                                      targetFramework,
                                                      item => new NuGetFramework(item.TargetFramework));

            var frameworkAssemblies = NuGetFrameworkUtility.GetNearest(nuspecReader.GetFrameworkReferenceGroups(),
                                                             targetFramework,
                                                             item => item.TargetFramework);

            return GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        public static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework, 
                                                               PackageDependencyGroup dependencies, 
                                                               FrameworkSpecificGroup frameworkAssemblies)
        {
            var libraryDependencies = new List<LibraryDependency>();

            if (dependencies != null)
            {
                foreach (var d in dependencies.Packages)
                {
                    libraryDependencies.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = d.Id,
                            VersionRange = d.VersionRange == null ? null : new NuGetVersionRange(d.VersionRange)
                        }
                    });
                }
            }

            if (frameworkAssemblies == null)
            {
                return libraryDependencies;
            }

            if (frameworkAssemblies.TargetFramework.AnyPlatform && !targetFramework.IsDesktop())
            {
                // REVIEW: This isn't 100% correct since none *can* mean 
                // any in theory, but in practice it means .NET full reference assembly
                // If there's no supported target frameworks and we're not targeting
                // the desktop framework then skip it.

                // To do this properly we'll need all reference assemblies supported
                // by each supported target framework which isn't always available.
                return libraryDependencies;
            }

            foreach (var name in frameworkAssemblies.Items)
            {
                libraryDependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = name,
                        IsGacOrFrameworkReference = true
                    }
                });
            }

            return libraryDependencies;
        }

        private LocalPackageInfo FindCandidate(string name, NuGetVersionRange versionRange)
        {
            var packages = _repository.FindPackagesById(name);

            return packages.FindBestMatch(versionRange, info => info?.Version);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Extensions;
using NuGet.Versioning.Extensions;

namespace NuGet.ProjectModel
{
    public class LockFileDependencyProvider : IDependencyProvider
    {
        private readonly ILookup<string, LockFileLibrary> _libraries;

        public LockFileDependencyProvider(LockFile lockFile)
        {
            _libraries = lockFile.Libraries.ToLookup(l => l.Name);
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            var library = FindCandidate(libraryRange);

            if (library != null)
            {
                var description = new LibraryDescription
                {
                    LibraryRange = libraryRange,
                    Identity = new Library
                    {
                        Name = library.Name,
                        Version = library.Version
                    },
                    Type = LibraryDescriptionTypes.Package,
                    Dependencies = GetDependencies(library, targetFramework)
                };

                description.Items["package"] = library;
                description.Items["files"] = library.Files;

                return description;
            }

            return null;
        }

        private IList<LibraryDependency> GetDependencies(LockFileLibrary library, NuGetFramework targetFramework)
        {
            var dependencies = NuGetFrameworkUtility.GetNearest(library.DependencyGroups,
                                                      targetFramework,
                                                      item => new NuGetFramework(item.TargetFramework));

            var frameworkAssemblies = NuGetFrameworkUtility.GetNearest(library.FrameworkReferenceGroups,
                                                             targetFramework,
                                                             item => item.TargetFramework);

            return NuGetDependencyResolver.GetDependencies(targetFramework, dependencies, frameworkAssemblies);
        }

        private LockFileLibrary FindCandidate(LibraryRange libraryRange)
        {
            var packages = _libraries[libraryRange.Name];

            return packages.FindBestMatch(libraryRange.VersionRange, library => library?.Version);
        }
    }
}
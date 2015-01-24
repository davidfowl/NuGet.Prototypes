using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Evaluation;
using NuGet.DependencyResolver;
using NuGet.Frameworks;
using NuGet.Packaging.Extensions;
using NuGet.Versioning;
using NuGet.Versioning.Extensions;
using NuGetProject = Microsoft.Framework.Runtime.Project;

namespace NuGet.MSBuild
{
    public class MSBuildDependencyProvider : IDependencyProvider
    {
        private readonly ProjectCollection _projectCollection;

        public MSBuildDependencyProvider(ProjectCollection projectCollection)
        {
            _projectCollection = projectCollection;
        }

        public LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework)
        {
            if (libraryRange.IsGacOrFrameworkReference)
            {
                return null;
            }

            var project = _projectCollection.LoadedProjects.FirstOrDefault(p => string.Equals(p.GetPropertyValue("ProjectGuid"), libraryRange.Name));

            if (project == null)
            {
                return null;
            }

            var projectInstance = project.CreateProjectInstance();

            var projectReferences = projectInstance.GetItems("ProjectReference")
                                   .Select(p => p.GetMetadataValue("FullPath"))
                                   .ToList();

            var dependencies = new List<LibraryDependency>();

            foreach (var projectReference in projectReferences)
            {
                var referencedProject = _projectCollection.LoadProject(projectReference);
                var referencedProjectInstance = referencedProject.CreateProjectInstance();
                dependencies.Add(new LibraryDependency
                {
                    LibraryRange = new LibraryRange
                    {
                        Name = referencedProjectInstance.GetPropertyValue("ProjectGuid"),
                        VersionRange = new NuGetVersionRange(new NuGetVersion(new Version(1, 0)))
                    },
                });
            }

            NuGetProject nugetProject;
            var projectDirectory = Path.GetDirectoryName(project.ProjectFileLocation.File);

            if (NuGetProject.TryGetProject(projectDirectory, out nugetProject))
            {
                // Grab dependencies from here too
                var targetFrameworkInfo = nugetProject.GetTargetFramework(targetFramework);

                dependencies.AddRange(nugetProject.Dependencies);
                dependencies.AddRange(targetFrameworkInfo.Dependencies);
            }

            return new LibraryDescription
            {
                LibraryRange = libraryRange,
                Identity = new Library
                {
                    Name = libraryRange.Name,
                    Version = new NuGetVersion(new Version(1, 0)) // TODO: Make up something better
                },
                Path = project.ProjectFileLocation.File,
                Dependencies = dependencies
            };
        }
    }
}

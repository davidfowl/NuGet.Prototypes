using System;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging.Build;
using NuGet.Packaging.Extensions;
using NuGet.ProjectModel;

namespace NuGet3
{
    public class PackCommand
    {
        public string ProjectFile { get; set; }

        public ILogger Logger { get; set; }

        public bool Execute()
        {
            var builder = new PackageBuilder();
            builder.RelativePathRoot = Path.GetDirectoryName(ProjectFile);

            Project project;
            if (!ProjectReader.TryReadProject(ProjectFile, out project))
            {
                Logger.WriteError("Unable to find project.json".Red());
                return false;
            }

            // TODO: Validation?
            builder.Manifest.SetMetadataValue("id", project.Name);
            builder.Manifest.SetMetadataValue("version", project.Version);
            builder.Manifest.SetMetadataValue("description", project.Description);
            builder.Manifest.SetMetadataValue("authors", project.Authors.Any() ? project.Authors : new[] { "NuGet" });
            builder.Manifest.SetMetadataValue("projectUrl", project.ProjectUrl);
            builder.Manifest.SetMetadataValue("iconUrl", project.IconUrl);
            builder.Manifest.SetMetadataValue("licenseUrl", project.LicenseUrl);
            builder.Manifest.SetMetadataValue("requireLicenseAcceptance", project.RequireLicenseAcceptance);
            builder.Manifest.SetMetadataValue("owners", project.Owners);
            builder.Manifest.SetMetadataValue("copyright", project.Copyright);
            builder.Manifest.SetMetadataValue("tags", project.Tags);

            builder.Manifest.DefineDependencies(s =>
            {
                foreach (var framework in project.TargetFrameworks)
                {
                    foreach (var dependency in project.Dependencies.Concat(framework.Dependencies))
                    {
                        if (!dependency.Type.Contains(LibraryDependencyTypeFlag.BecomesNupkgDependency) ||
                            dependency.LibraryRange.Type == LibraryTypes.FrameworkOrGacAssembly)
                        {
                            continue;
                        }

                        s.DefineEntry(d =>
                        {
                            d.SetMetadataValue("id", dependency.Name);
                            d.SetMetadataValue("version", dependency.LibraryRange.VersionRange.MinVersion);

                            if (!dependency.Type.Contains(LibraryDependencyTypeFlag.MainReference))
                            {
                                d.SetMetadataValue("type", "private");
                            }

                            d.SetMetadataValue("targetFramework", framework.FrameworkName);
                        });
                    }
                }
            });

            builder.Manifest.DefineFrameworkAssemblies(s =>
            {
                foreach (var framework in project.TargetFrameworks)
                {
                    foreach (var dependency in framework.Dependencies.Where(d => d.LibraryRange.Type == LibraryTypes.FrameworkOrGacAssembly))
                    {
                        s.DefineEntry(a =>
                        {
                            a.SetMetadataValue("name", dependency.Name);
                            a.SetMetadataValue("targetFramework", framework.FrameworkName);
                        });
                    }
                }
            });

            string configuration = "Debug";
            string outputPath = Path.Combine(builder.RelativePathRoot, "bin", configuration);

            foreach (var framework in project.TargetFrameworks)
            {
                var shortName = framework.FrameworkName.GetShortFolderName();
                if (shortName.StartsWith("aspnet"))
                {
                    shortName += "0";
                }
                var src = Path.Combine(outputPath, shortName, project.Name + ".dll");
                var target = string.Format("lib/{0}/{1}.dll", shortName, project.Name);
                builder.AddFile(src, target);
            }

            var path = Path.Combine(outputPath, project.Name + "." + project.Version + ".nupkg");

            using (var stream = File.Create(path))
            {
                builder.Save(stream);
            }

            Console.WriteLine(path);

            return true;
        }
    }
}
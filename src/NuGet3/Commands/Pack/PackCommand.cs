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
        public string ProjectDirectory { get; set; }

        public ILogger Logger { get; set; }

        public bool Execute()
        {
            var builder = new PackageBuilder();
            builder.RelativePathRoot = Directory.GetCurrentDirectory();

            Project project;
            if (!ProjectReader.TryReadProject(ProjectDirectory, out project))
            {
                Logger.WriteError("Unable to find project.json".Red());
                return false;
            }

            // TODO: Validation?
            builder.Manifest.SetMetadataValue("id", project.Name);
            builder.Manifest.SetMetadataValue("version", project.Version);
            builder.Manifest.SetMetadataValue("description", project.Description);
            builder.Manifest.SetMetadataValue("projectUrl", project.ProjectUrl);
            builder.Manifest.SetMetadataValue("iconUrl", project.IconUrl);
            builder.Manifest.SetMetadataValue("licenseUrl", project.LicenseUrl);
            builder.Manifest.SetMetadataValue("requireLicenseAcceptance", project.RequireLicenseAcceptance);
            builder.Manifest.SetMetadataValue("author", project.Authors);
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
                            dependency.LibraryRange.IsGacOrFrameworkReference)
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
                    foreach (var dependency in framework.Dependencies.Where(d => d.LibraryRange.IsGacOrFrameworkReference))
                    {
                        s.DefineEntry(a =>
                        {
                            a.SetMetadataValue("name", dependency.Name);
                            a.SetMetadataValue("targetFramework", framework.FrameworkName);
                        });
                    }
                }
            });

            var stream = Console.OpenStandardOutput();
            var nuspecFormatter = new NuSpecFormatter();
            nuspecFormatter.Save(builder.Manifest, stream);
            Console.WriteLine();
            var json = new JsonFormatter();
            json.Write(builder.Manifest, stream);

            Console.WriteLine();

            builder.AddFile("bin/Debug/Foo.dll", "lib/net45/Foo.dll", version: 0);
            //builder.AddFile("bin/Debug/Foo.pdb", "lib/net45/Foo.pdb", version: 1);
            //builder.AddFilePattern("native/**/*.*", "native");

            return true;
        }
    }
}
using System;
using System.IO;
using NuGet.Packaging.Build;
using NuGet.Versioning;

namespace NuGet3
{
    public class PackCommand
    {
        public bool Execute()
        {
            var builder = new PackageBuilder();
            builder.RelativePathRoot = Directory.GetCurrentDirectory();

            // TODO: Validation?
            builder.Manifest.SetMetadataValue("id", "Hello");
            builder.Manifest.SetMetadataValue("version", "1.0.0-alpha3");
            builder.Manifest.SetMetadataValue("description", "This is a test package");
            builder.Manifest.SetMetadataValues("author", new[] { "David" });
            builder.Manifest.SetMetadataValues("owners", new[] { "owner1" });

            builder.Manifest.DefineDependencies(s =>
            {
                s.DefineEntry(d =>
                {
                    d.SetMetadataValue("id", "Newtonsoft.Json");
                    d.SetMetadataValue("version", "1.0.0-beta1");
                    d.SetMetadataValue("type", "private");
                    d.SetMetadataValue("targetFramework", "net45");
                });
            });

            builder.Manifest.DefineFrameworkAssemblies(s =>
            {
                s.DefineEntry(a =>
                {
                    a.SetMetadataValue("name", "System.Web");
                    a.SetMetadataValue("targetFramework", "net45");
                });
            });

            var stream = Console.OpenStandardOutput();
            var xml = new XmlFormatter();
            xml.Save(builder.Manifest, stream);
            Console.WriteLine();
            var json = new JsonFormatter();
            json.Write(builder.Manifest, stream);

            Console.WriteLine();

            //builder.AddFile("bin/Debug/Foo.dll", "lib/net45/Foo.dll", version: 0);
            //builder.AddFile("bin/Debug/Foo.pdb", "lib/net45/Foo.pdb", version: 1);
            //builder.AddFilePattern("native/**/*.*", "native");

            return true;
        }
    }
}
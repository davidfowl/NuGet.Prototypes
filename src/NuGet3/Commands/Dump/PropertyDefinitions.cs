using NuGet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.ContentModel;
using NuGet.Frameworks;

namespace NuGet3
{
    public class PropertyDefinitions
    {
        public PropertyDefinitions()
        {
            Definitions = new Dictionary<string, ContentPropertyDefinition>
                {
                    { "arch", _arch },
                    { "language", _language },
                    { "tfm", _targetFramework },
                    { "tpm", _targetPlatform },
                    { "assembly", _assembly },
                    { "dynamicLibrary", _dynamicLibrary },
                    { "resources", _resources },
                    { "locale", _locale },
                    { "any", _any },
                };
        }

        public IDictionary<string, ContentPropertyDefinition> Definitions { get; }

        ContentPropertyDefinition _arch = new ContentPropertyDefinition
        {
            Table =
                {
                    { "x86", "x86" },
                    { "x64", "amd64" },
                    { "amd64", "amd64" },
                    { "arm64", "arm64" },
                    { "universal","anyCpu" },
                }
        };

        ContentPropertyDefinition _language = new ContentPropertyDefinition
        {
            Table =
                {
                    { "cs", "CSharp" },
                    { "vb", "Visual Basic" },
                    { "fs", "FSharp" },
                }
        };

        ContentPropertyDefinition _targetFramework = new ContentPropertyDefinition
        {
            Table =
                {
                    { "aspnet50", new FrameworkName("ASP.NET,Version=5.0") },
                    { "aspnetcore50", new FrameworkName("ASP.NETCore,Version=5.0") },
                    { "any", new FrameworkName("ContractBased,Version=1.0") },
                    { "monoandroid", new FrameworkName("MonoAndroid,Version=0.0") },
                    { "monotouch", new FrameworkName("MonoTouch,Version=0.0") },
                    { "monomac", new FrameworkName("MonoMac,Version=0.0") },
                },
            Parser = TargetFrameworkName_Parser,
            OnIsCriteriaSatisfied = TargetFrameworkName_IsCriteriaSatisfied
        };

        ContentPropertyDefinition _targetPlatform = new ContentPropertyDefinition
        {
            Table =
                {
                    { "win81", new FrameworkName("Windows", new Version(8, 1)) },
                    { "win8", new FrameworkName("Windows", new Version(8, 0)) },
                    { "win7", new FrameworkName("Windows", new Version(7, 0)) },
                    { "windows", new FrameworkName("Windows", new Version(0, 0)) },
                    { "wp8", new FrameworkName("WindowsPhone", new Version(8, 0)) },
                    { "wp81", new FrameworkName("WindowsPhone", new Version(8, 1)) },
                    { "uap10", new FrameworkName("UAP", new Version(10, 0)) },
                    { "darwin", new FrameworkName("Darwin", new Version(0, 0)) },
                },
            OnIsCriteriaSatisfied = TargetPlatformName_IsCriteriaSatisfied,
        };

        ContentPropertyDefinition _assembly = new ContentPropertyDefinition
        {
            FileExtensions = { ".dll" }
        };

        ContentPropertyDefinition _dynamicLibrary = new ContentPropertyDefinition
        {
            FileExtensions = { ".dll", ".dylib", ".so" }
        };

        ContentPropertyDefinition _resources = new ContentPropertyDefinition
        {
            FileExtensions = { ".resources.dll" }
        };

        ContentPropertyDefinition _locale = new ContentPropertyDefinition
        {
            Parser = Locale_Parser,
        };

        ContentPropertyDefinition _any = new ContentPropertyDefinition
        {
            Parser = name => name
        };


        internal static object Locale_Parser(string name)
        {
            if (name.Length == 2)
            {
                return name;
            }
            else if (name.Length >= 4 && name[2] == '-')
            {
                return name;
            }

            return null;
        }

        internal static object TargetFrameworkName_Parser(string name)
        {
            if (name.Contains('.') || name.Contains('/'))
            {
                return null;
            }

            if (name.StartsWith("portable-", StringComparison.OrdinalIgnoreCase))
            {
                // return new NetPortableProfileWithToString(NetPortableProfile.Parse(name.Substring("portable-".Length)));
            }

            if (name == "contract")
            {
                return null;
            }

            var result = NuGetFramework.Parse(name);

            if (result != NuGetFramework.UnsupportedFramework)
            {
                return result;
            }

            return new NuGetFramework(name, new Version());
        }

        internal static bool TargetFrameworkName_IsCriteriaSatisfied(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as NuGetFramework;
            var availableFrameworkName = available as NuGetFramework;

            if (criteriaFrameworkName != null && availableFrameworkName != null)
            {
                if (!string.Equals(criteriaFrameworkName.Framework, availableFrameworkName.Framework, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (NormalizeVersion(criteriaFrameworkName.Version) < NormalizeVersion(availableFrameworkName.Version))
                {
                    return false;
                }

                if (!string.Equals(criteriaFrameworkName.Profile, availableFrameworkName.Profile, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                return true;
            }

            /*var criteriaPortableProfile = criteria as NetPortableProfileWithToString;
            var availablePortableProfile = available as NetPortableProfileWithToString;
            if (criteriaPortableProfile != null && availablePortableProfile != null)
            {
                if (availablePortableProfile.Profile.IsCompatibleWith(criteriaPortableProfile.Profile))
                {
                    return true;
                }
            }*/

            return false;
        }

        internal static bool TargetPlatformName_IsCriteriaSatisfied(object criteria, object available)
        {
            var criteriaFrameworkName = criteria as FrameworkName;
            var availableFrameworkName = available as FrameworkName;

            if (criteriaFrameworkName != null && availableFrameworkName != null)
            {
                if (!String.Equals(criteriaFrameworkName.Identifier, availableFrameworkName.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (NormalizeVersion(criteriaFrameworkName.Version) < NormalizeVersion(availableFrameworkName.Version))
                {
                    return false;
                }

                return true;
            }
            return false;
        }

        internal static Version NormalizeVersion(Version version)
        {
            return new Version(version.Major,
                               version.Minor,
                               Math.Max(version.Build, 0),
                               Math.Max(version.Revision, 0));
        }
    }

    /*public class NetPortableProfileWithToString
    {
        public NetPortableProfileWithToString(NetPortableProfile profile)
        {
            Profile = profile;
        }

        public NetPortableProfile Profile { get; }

        public override string ToString()
        {
            return "portable-" + Profile.CustomProfileString;
        }

        public override int GetHashCode()
        {
            return Profile.CustomProfileString?.GetHashCode() ?? 0;
        }

        public override bool Equals(object obj)
        {
            return Profile.CustomProfileString.Equals((obj as NetPortableProfileWithToString)?.Profile?.CustomProfileString);
        }
    }*/
}
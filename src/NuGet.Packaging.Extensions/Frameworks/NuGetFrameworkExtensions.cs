using System;
using NuGet.Frameworks;

namespace NuGet.Packaging.Extensions.Frameworks
{
    public static class NuGetFrameworkExtensions
    {
        public static bool IsDesktop(this NuGetFramework framework)
        {
            return framework.DotNetFrameworkName.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase);
        }

    }
}
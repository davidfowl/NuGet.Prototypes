using System;

namespace NuGet.Versioning.Extensions
{
    public enum NuGetVersionFloatBehavior
    {
        None,
        Prerelease,
        Revision,
        Build,
        Minor,
        Major
    }

}
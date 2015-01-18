using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;
using NuGet.Packaging.Extensions;
using NuGet.Frameworks;

namespace NuGet3
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, NuGetFramework targetFramework);
        Task CopyToAsync(WalkProviderMatch match, Stream stream);
    }

}
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NuGet.Packaging.Extensions;
using NuGet.Frameworks;

namespace NuGet.Resolver
{
    public interface IWalkProvider
    {
        bool IsHttp { get; }

        Task<RemoteResolveResult> FindLibrary(LibraryRange libraryRange, NuGetFramework targetFramework);
        Task<IEnumerable<LibraryDependency>> GetDependencies(RemoteResolveResult match, NuGetFramework targetFramework);
        Task CopyToAsync(RemoteResolveResult match, Stream stream);
    }

}
using NuGet.Packaging.Extensions;

namespace NuGet.Resolver
{
    public class ResolveResult
    {
        public LibraryDescription LibraryDescription { get; set; }
        public IDependencyProvider DependencyProvider { get; set; }
    }
}
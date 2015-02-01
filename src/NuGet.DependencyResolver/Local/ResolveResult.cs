using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class ResolveResult
    {
        public LibraryDescription LibraryDescription { get; set; }
        public IDependencyProvider DependencyProvider { get; set; }
    }
}
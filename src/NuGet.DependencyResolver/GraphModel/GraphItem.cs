using System.Diagnostics;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    [DebuggerDisplay("{Key}")]
    public class GraphItem<TItem>
    {
        public GraphItem(Library key)
        {
            Key = key;
        }

        public Library Key { get; set; }
        public TItem Data { get; set; }
    }
}
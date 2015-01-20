using System;
using System.Collections.Generic;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public class GraphNode
    {
        public GraphNode()
        {
            InnerNodes = new List<GraphNode>();
            Disposition = Disposition.Acceptable;
        }

        public LibraryRange Key { get; set; }
        public GraphItem Item { get; set; }
        public GraphNode OuterNode { get; set; }
        public IList<GraphNode> InnerNodes { get; private set; }

        public Disposition Disposition { get; set; }

        public override string ToString()
        {
            return (Item?.Key ?? Key) + " " + Disposition;
        }
    }
}
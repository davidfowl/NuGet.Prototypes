using System;
using System.Collections.Generic;

namespace NuGet.DependencyResolver
{
    public static class GraphOperations
    {
        public static bool TryResolveConflicts<TItem>(this GraphNode<TItem> root)
        {
            // now we walk the tree as often as it takes to determine 
            // which paths are accepted or rejected, based on conflicts occuring
            // between cousin packages

            var patience = 1000;
            var incomplete = true;
            while (incomplete && --patience != 0)
            {
                // Create a picture of what has not been rejected yet
                var tracker = new Tracker<TItem>();

                root.ForEach(true, (node, state) =>
                {
                    if (!state || node.Disposition == Disposition.Rejected)
                    {
                        // Mark all nodes as rejected if they aren't already marked
                        node.Disposition = Disposition.Rejected;
                        return false;
                    }

                    tracker.Track(node.Item);
                    return true;
                });

                // Inform tracker of ambiguity beneath nodes that are not resolved yet
                // between:
                // a1->b1->d1->x1
                // a1->c1->d2->z1
                // first attempt
                //  d1/d2 are considered disputed 
                //  x1 and z1 are considered ambiguous
                //  d1 is rejected
                // second attempt
                //  d1 is rejected, d2 is accepted
                //  x1 is no longer seen, and z1 is not ambiguous
                //  z1 is accepted

                root.ForEach("Walking", (node, state) =>
                {
                    if (node.Disposition == Disposition.Rejected)
                    {
                        return "Rejected";
                    }

                    if (state == "Walking" && tracker.IsDisputed(node.Item))
                    {
                        return "Ambiguous";
                    }

                    if (state == "Ambiguous")
                    {
                        tracker.MarkAmbiguous(node.Item);
                    }

                    return state;
                });

                // Now mark unambiguous nodes as accepted or rejected
                root.ForEach(true, (node, state) =>
                {
                    if (!state || node.Disposition == Disposition.Rejected)
                    {
                        return false;
                    }

                    if (tracker.IsAmbiguous(node.Item))
                    {
                        return false;
                    }

                    if (node.Disposition == Disposition.Acceptable)
                    {
                        node.Disposition = tracker.IsBestVersion(node.Item) ? Disposition.Accepted : Disposition.Rejected;
                    }

                    return node.Disposition == Disposition.Accepted;
                });

                incomplete = false;

                root.ForEach(node => incomplete |= node.Disposition == Disposition.Acceptable);
            }

            return incomplete;
        }
        

        public static void ForEach<TItem, TState>(this GraphNode<TItem> root, TState state, Func<GraphNode<TItem>, TState, TState> visitor)
        {
            // breadth-first walk of Node tree

            var queue = new Queue<Tuple<GraphNode<TItem>, TState>>();
            queue.Enqueue(Tuple.Create(root, state));
            while (queue.Count > 0)
            {
                var work = queue.Dequeue();
                var innerState = visitor(work.Item1, work.Item2);
                foreach (var innerNode in work.Item1.InnerNodes)
                {
                    queue.Enqueue(Tuple.Create(innerNode, innerState));
                }
            }
        }

        public static void ForEach<TItem>(this GraphNode<TItem> root, Action<GraphNode<TItem>> visitor)
        {
            // breadth-first walk of Node tree, without TState parameter
            ForEach(root, 0, (node, _) =>
            {
                visitor(node);
                return 0;
            });
        }
    }
}
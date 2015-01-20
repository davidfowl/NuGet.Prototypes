using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.DependencyResolver
{
    public class Tracker
    {
        class Entry
        {
            public Entry()
            {
                List = new HashSet<GraphItem>();
            }

            public HashSet<GraphItem> List { get; set; }

            public bool Ambiguous { get; set; }
        }

        readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();

        private Entry GetEntry(GraphItem item)
        {
            Entry itemList;
            if (!_entries.TryGetValue(item.Key.Name, out itemList))
            {
                itemList = new Entry();
                _entries[item.Key.Name] = itemList;
            }
            return itemList;
        }

        public void Track(GraphItem item)
        {
            var entry = GetEntry(item);
            if (!entry.List.Contains(item))
            {
                entry.List.Add(item);
            }
        }

        public bool IsDisputed(GraphItem item)
        {
            return GetEntry(item).List.Count > 1;
        }

        public bool IsAmbiguous(GraphItem item)
        {
            return GetEntry(item).Ambiguous;
        }

        public void MarkAmbiguous(GraphItem item)
        {
            GetEntry(item).Ambiguous = true;
        }

        public bool IsBestVersion(GraphItem item)
        {
            var entry = GetEntry(item);
            return entry.List.All(known => item.Key.Version >= known.Key.Version);
        }
    }
}
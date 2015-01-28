using System;
using System.Collections.Generic;
using System.Linq;

namespace NuGet.Packaging.Build
{
    public class Metadata
    {
        private readonly Dictionary<string, object> _metadata = new Dictionary<string, object>();

        public IEnumerable<KeyValuePair<string, object>> GetValues()
        {
            return _metadata;
        }

        private IEnumerable<string> GetMetadataValues(string name)
        {
            return GetMetadataValue(name) as IEnumerable<string> ?? Enumerable.Empty<string>();
        }

        public object GetMetadataValue(string name)
        {
            object value;
            if (_metadata.TryGetValue(name, out value))
            {
                return value;
            }
            return null;
        }

        public void SetMetadataValues(string name, IEnumerable<string> values)
        {
            _metadata[name] = values;
        }

        public void SetMetadataValue(string name, string value)
        {
            _metadata[name] = value;
        }
    }
}
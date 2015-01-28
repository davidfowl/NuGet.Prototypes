using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NuGet.Packaging.Build
{
    public class JsonFormatter
    {
        public MetadataBuilder Read(Stream stream)
        {
            return null;
        }

        public void Write(MetadataBuilder builder, Stream stream)
        {
            var document = new JObject();
            
            foreach (var pair in builder.GetValues())
            {
                document[pair.Key] = JToken.FromObject(pair.Value);
            }
            
            foreach (var section in builder.GetSections())
            {
                var sectionEl = new JObject();
                document[section.Name] = sectionEl;

                if (string.IsNullOrEmpty(section.GroupByProperty))
                {
                    foreach (var item in section.GetEntries())
                    {
                        var el = new JObject();
                        sectionEl[section.ItemName] = el;
                        foreach (var pair in item.GetValues())
                        {
                            el[pair.Key] = JToken.FromObject(pair.Value);
                        }
                    }
                }
                else
                {
                    foreach (var group in section.GetEntries().GroupBy(s => s.GetMetadataValue(section.GroupByProperty)))
                    {
                        var groupEl = new JObject();
                        sectionEl[group.Key] = groupEl;

                        foreach (var item in group)
                        {
                            var el = new JObject();
                            groupEl[section.ItemName] = el;
                            foreach (var pair in item.GetValues())
                            {
                                if (string.Equals(pair.Key, section.GroupByProperty, StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }

                                el[pair.Key] = JToken.FromObject(pair.Value);
                            }
                        }
                    }
                }
            }

            var sw = new StreamWriter(stream) { AutoFlush = true };
            sw.Write(document.ToString(Formatting.Indented));
        }
    }
}
using System;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public class LanguageServices
    {
        public LanguageServices(string name, TypeInformation projectExportProvider)
        {
            Name = name;
            ProjectReferenceProvider = projectExportProvider;
        }

        public string Name { get; private set; }

        public TypeInformation ProjectReferenceProvider { get; private set; }
    }
}
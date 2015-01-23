
using NuGet.ContentModel;

namespace NuGet3
{
    public class PatternDefinitions
    {
        public PropertyDefinitions Properties { get; }
        public ContentPatternDefinition CompileTimeAssemblies { get; }
        public ContentPatternDefinition ManagedAssemblies { get; }
        public ContentPatternDefinition AheadOfTimeAssemblies { get; }
        public ContentPatternDefinition ResourceAssemblies { get; }
        public ContentPatternDefinition NativeLibraries { get; }

        public PatternDefinitions()
        {
            Properties = new PropertyDefinitions();

            ManagedAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "lib/{tfm}.{tpm}/{any?}",
                    "lib/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "lib/{tfm}.{tpm}/{assembly}",
                    "lib/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            CompileTimeAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "ref/{tfm}.{tpm}/{any?}",
                    "ref/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "ref/{tfm}.{tpm}/{assembly}",
                    "ref/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            AheadOfTimeAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "aot/{tfm}.{tpm}/{any?}",
                    "aot/{tfm}/{any?}",
                },
                PathPatterns =
                {
                    "aot/{tfm}.{tpm}/{assembly}",
                    "aot/{tfm}/{assembly}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            ResourceAssemblies = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "lib/{tfm}.{tpm}/{locale?}/{resources?}",
                    "lib/{tfm}/{locale?}/{resources?}",
                },
                PathPatterns =
                {
                    "lib/{tfm}.{tpm}/{locale}/{resources}",
                    "lib/{tfm}/{locale}/{resources}",
                },
                PropertyDefinitions = Properties.Definitions,
            };

            NativeLibraries = new ContentPatternDefinition
            {
                GroupPatterns =
                {
                    "native/{tfm}.{tpm}/{arch?}/{any?}",
                    "native/{tpm}/{arch?}/{any?}",
                },
                PathPatterns =
                {
                    "native/{tfm}.{tpm}/{arch}/{dynamicLibrary}",
                    "native/{tpm}/{arch}/{dynamicLibrary}",
                },
                PropertyDefinitions = Properties.Definitions,
            };
        }
    }
}
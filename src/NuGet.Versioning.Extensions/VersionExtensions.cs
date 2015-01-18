using System;

namespace NuGet.Versioning.Extensions
{
    public static class VersionExtensions
    {
        public static bool IsBetter(
            this NuGetVersionRange ideal,
            NuGetVersion current,
            NuGetVersion considering)
        {
            if (considering == null)
            {
                // skip nulls
                return false;
            }

            if (!ideal.EqualsFloating(considering) && considering < ideal.MinVersion)
            {
                // Don't use anything that can't be satisfied
                return false;
            }

            /*
            Come back to this later
            if (ideal.VersionFloatBehavior == SemanticVersionFloatBehavior.None &&
                considering != ideal.MinVersion)
            {
                return false;
            }
            */

            if (current == null)
            {
                // always use version when it's the first valid
                return true;
            }

            if (ideal.EqualsFloating(current) &&
                ideal.EqualsFloating(considering))
            {
                // favor higher version when they both match a floating pattern
                return current < considering;
            }

            // Favor lower versions
            return current > considering;
        }
    }


}

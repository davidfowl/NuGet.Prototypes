using System;
using System.Linq;
using NuGet.Frameworks;

namespace NuGet.ProjectModel
{
    public static class ProjectExtensions
    {
        public static TargetFrameworkInformation GetTargetFramework(this Project project, NuGetFramework targetFramework)
        {
            var reducer = new FrameworkReducer();
            var frameworks = project.TargetFrameworks.ToDictionary(g => g.FrameworkName);

            var nearest = reducer.GetNearest(targetFramework, frameworks.Keys);

            if (nearest != null)
            {
                return frameworks[nearest];
            }

            return new TargetFrameworkInformation();
        }
    }
}
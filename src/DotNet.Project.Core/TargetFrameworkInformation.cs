// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using NuGet.Packaging.Extensions;
using NuGet.Frameworks;

namespace Microsoft.Framework.Runtime
{
    public class TargetFrameworkInformation
    {
        public NuGetFramework FrameworkName { get; set; }

        public IList<LibraryDependency> Dependencies { get; set; }

        public string WrappedProject { get; set; }

        public string AssemblyPath { get; set; }

        public string PdbPath { get; set; }
    }
}
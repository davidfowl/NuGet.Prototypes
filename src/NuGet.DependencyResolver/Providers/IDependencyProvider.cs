// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;
using NuGet.Packaging.Extensions;

namespace NuGet.DependencyResolver
{
    public interface IDependencyProvider
    {
        LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework);
    }
}

// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Frameworks;

namespace NuGet.Packaging.Extensions
{
    public interface IDependencyProvider
    {
        LibraryDescription GetDescription(LibraryRange libraryRange, NuGetFramework targetFramework);
    }
}

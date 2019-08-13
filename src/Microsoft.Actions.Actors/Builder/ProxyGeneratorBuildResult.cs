// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System;

    internal class ProxyGeneratorBuildResult : BuildResult
    {
        public ProxyGeneratorBuildResult(CodeBuilderContext buildContext)
            : base(buildContext)
        {
        }

        public Type ProxyType { get; set; }

        public Type ProxyActivatorType { get; set; }

        public ProxyGenerator ProxyGenerator { get; set; }
    }
}

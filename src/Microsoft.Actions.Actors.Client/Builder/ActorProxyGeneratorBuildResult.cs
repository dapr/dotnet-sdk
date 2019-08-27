// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client.Builder
{
    using Microsoft.Actions.Actors.Builder;
    using System;

    internal class ActorProxyGeneratorBuildResult : BuildResult
    {
        public ActorProxyGeneratorBuildResult(CodeBuilderContext buildContext)
            : base(buildContext)
        {
        }

        public Type ProxyType { get; set; }

        public Type ProxyActivatorType { get; set; }

        public ActorProxyGenerator ProxyGenerator { get; set; }
    }
}

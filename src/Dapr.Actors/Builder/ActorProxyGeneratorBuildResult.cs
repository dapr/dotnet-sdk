// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
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

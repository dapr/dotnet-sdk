// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;

    internal class MethodDispatcherBuildResult : BuildResult
    {
        public MethodDispatcherBuildResult(CodeBuilderContext buildContext)
            : base(buildContext)
        {
        }

        public Type MethodDispatcherType { get; set; }

        public ActorMethodDispatcherBase MethodDispatcher { get; set; }
    }
}

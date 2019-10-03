// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
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

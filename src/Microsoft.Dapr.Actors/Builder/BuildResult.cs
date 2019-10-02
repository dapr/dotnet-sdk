// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Dapr.Actors.Builder
{
    internal class BuildResult
    {
        protected BuildResult(CodeBuilderContext buildContext)
        {
            this.BuildContext = buildContext;
        }

        public CodeBuilderContext BuildContext { get; }
    }
}

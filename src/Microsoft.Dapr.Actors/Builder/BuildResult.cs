// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
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

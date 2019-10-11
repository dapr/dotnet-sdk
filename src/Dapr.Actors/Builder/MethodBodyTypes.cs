// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Builder
{
    using System;

    internal class MethodBodyTypes
    {
        public Type RequestBodyType { get; set; }

        public Type ResponseBodyType { get; set; }

        public bool HasCancellationTokenArgument { get; set; }
    }
}

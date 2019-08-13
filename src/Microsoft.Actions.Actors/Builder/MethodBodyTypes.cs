// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System;

    internal class MethodBodyTypes
    {
        public Type RequestBodyType { get; set; }

        public Type ResponseBodyType { get; set; }

        public bool HasCancellationTokenArgument { get; set; }
    }
}

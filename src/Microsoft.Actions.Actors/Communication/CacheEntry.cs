// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class CacheEntry
    {
        public CacheEntry(
            IActorMessageBodySerializer messageBodySerializer)
        {
            this.MessageBodySerializer = messageBodySerializer;
        }

        public IActorMessageBodySerializer MessageBodySerializer { get; }
    }
}

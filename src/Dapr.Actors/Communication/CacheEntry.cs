// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Actors.Communication
{
    internal class CacheEntry
    {
        public CacheEntry(
            IActorRequestMessageBodySerializer requestBodySerializer,
            IActorResponseMessageBodySerializer responseBodySerializer)
        {
            this.RequestMessageBodySerializer = requestBodySerializer;
            this.ResponseMessageBodySerializer = responseBodySerializer;
        }

        public IActorRequestMessageBodySerializer RequestMessageBodySerializer { get; }

        public IActorResponseMessageBodySerializer ResponseMessageBodySerializer { get; }
    }
}

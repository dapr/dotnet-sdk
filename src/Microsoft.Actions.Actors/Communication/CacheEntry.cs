// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication
{
    internal class CacheEntry
    {
        private readonly IActorCommunicationRequestMessageBodySerializer requestBodySerializer;
        private readonly IActorCommunicationResponseMessageBodySerializer responseBodySerializer;

        public CacheEntry(
            IActorCommunicationRequestMessageBodySerializer requestBodySerializer,
            IActorCommunicationResponseMessageBodySerializer responseBodySerializer)
        {
            this.requestBodySerializer = requestBodySerializer;
            this.responseBodySerializer = responseBodySerializer;
        }

        public IActorCommunicationRequestMessageBodySerializer RequestBodySerializer
        {
            get { return this.requestBodySerializer; }
        }

        public IActorCommunicationResponseMessageBodySerializer ResponseBodySerializer
        {
            get { return this.responseBodySerializer; }
        }
    }
}

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    internal class HttpActorCommunicationClient : IActorCommunicationClient
    {
        private readonly ActorMessageSerializersManager serializersManager;
        private readonly IActionsInteractor actionsInteractor;

        // we need to pass a cache of the serializers here rather than the known types,
        // the serializer cache should be maintained by the factor
        internal HttpActorCommunicationClient(
            ActorMessageSerializersManager serializersManager,
            IActionsInteractor actionsInteractor)
        {
            this.actionsInteractor = actionsInteractor;
            this.serializersManager = serializersManager;
        }

        public ActorId ActorId => throw new NotImplementedException();

        public async Task<IActorResponseMessage> RequestResponseAsync(
            IActorRequestMessage remotingRequestRequestMessage)
        {
            var requestMessageHeader = remotingRequestRequestMessage.GetHeader();

            var actorId = requestMessageHeader.ActorId.ToString();
            var method = requestMessageHeader.MethodName;
            var actorType = requestMessageHeader.ActorType;
            var interfaceId = requestMessageHeader.InterfaceId;

            var serializedHeader = this.serializersManager.GetHeaderSerializer()
                .SerializeRequestHeader(remotingRequestRequestMessage.GetHeader());

            var msgBodySeriaizer = this.serializersManager.GetRequestBodySerializer(interfaceId);
            var serializedMsgBody = msgBodySeriaizer.Serialize(remotingRequestRequestMessage.GetBody());

            var serializedHeaderBytes = serializedHeader.GetSendBytes();

            var serializedMsgBodyBuffers = serializedMsgBody.GetSendBytes();

            // Send Request
            var retval = (HttpResponseMessage)await this.actionsInteractor.InvokeActorMethod(actorId, actorType, method, serializedHeaderBytes, serializedMsgBodyBuffers);

            // TODO finalize on pending on response message format and test 
            // Need to come back once decided on response message
            // Get the http header and extract out expected actor response message header
            IActorResponseMessageHeader actorResponseMessageHeader = null;
            if (retval != null && retval.Headers != null)
            {
                IEnumerable<string> headerValues = null;
                
                // TODO Assert if expected header is not there
                if (retval.Headers.TryGetValues(Constants.RequestHeaderName, out headerValues))
                {
                    var header = headerValues.First();

                    var incomingHeader = new IncomingMessageHeader(new MemoryStream(Encoding.ASCII.GetBytes(header)));

                    // DeSerialize Actor Response Message Header
                    actorResponseMessageHeader =
                        this.serializersManager.GetHeaderSerializer()
                            .DeserializeResponseHeaders(
                                incomingHeader);
                }
            }

            // Get the http response message body content and extract out expected actor response message body
            IActorMessageBody actorResponseMessageBody = null;
            if (retval != null && retval.Content != null)
            {
                var responseMessageBody = await retval.Content.ReadAsStreamAsync();

                // Deserialize Actor Response Message Body
                var responseBodySerializer = this.serializersManager.GetRequestBodySerializer(interfaceId);

                actorResponseMessageBody =
                    responseBodySerializer.Deserialize(new IncomingMessageBody(responseMessageBody));
            }

            // TODO Either throw exception or return response body with null header and message body
            return new ActorResponseMessage(actorResponseMessageHeader, actorResponseMessageBody);
        }

        public void SendOneWay(IActorRequestMessage requestMessage)
        {
            throw new NotImplementedException();
        }
    }
}

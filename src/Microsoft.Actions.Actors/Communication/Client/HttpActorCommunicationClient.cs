// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Communication.Client
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Runtime;

    internal class HttpActorCommunicationClient : IActorCommunicationClient
    {
        private readonly ActorCommunicationMessageSerializersManager serializersManager;
        private readonly IActionsInteractor fabricTransportClient;

        // we need to pass a cache of the serializers here rather than the known types,
        // the serializer cache should be maintained by the factor
        internal HttpActorCommunicationClient(
            ActorCommunicationMessageSerializersManager serializersManager,
            IActionsInteractor fabricTransportClient)
        {
            this.fabricTransportClient = fabricTransportClient;
            this.serializersManager = serializersManager;
        }

        public ActorId ActorId => throw new NotImplementedException();

        public async Task<IResponseMessage> RequestResponseAsync(
            IRequestMessage remotingRequestRequestMessage)
        {
            var actorId = new ActorId("actorId");
            string method = "methodName";
            Type actorType = typeof(object);
            var interfaceId = remotingRequestRequestMessage.GetHeader().InterfaceId;
            var serializedHeader = this.serializersManager.GetHeaderSerializer()
                .SerializeRequestHeader(remotingRequestRequestMessage.GetHeader());
            var msgBodySeriaizer = this.serializersManager.GetRequestBodySerializer(interfaceId);
            var serializedMsgBody = msgBodySeriaizer.Serialize(remotingRequestRequestMessage.GetBody());

            var serializedHeaderBytes = serializedHeader.GetSendBytes();

            var serializedMsgBodyBuffers = serializedMsgBody.GetSendBytes();

            // Send Request
            await this.fabricTransportClient.InvokeActorMethod(actorId, actorType, method, serializedHeaderBytes, serializedMsgBodyBuffers);

            // TODO pending on response message format
            /* Need to come back once decided on response message
            var incomingHeader = (retval != null && retval.GetHeader() != null)
                    ? new IncomingMessageHeader(retval.GetHeader().GetRecievedStream())
                    : null;

            ////DeSerialize Response
            var header =
                this.serializersManager.GetHeaderSerializer()
                    .DeserializeResponseHeaders(
                        incomingHeader);

            if (header != null && header.TryGetHeaderValue("HasRemoteException", out var headerValue))
            {
                var isDeserialzied =
                    RemoteException.ToException(
                        retval.GetBody().GetRecievedStream(),
                        out var e);
                if (isDeserialzied)
                {
                    throw new AggregateException(e);
                }
                else
                {
                    throw new ServiceException(e.GetType().FullName, string.Format(
                        CultureInfo.InvariantCulture,
                        Remoting.SR.ErrorDeserializationFailure,
                        e.ToString()));
                }
            }

            var responseSerializer = this.serializersManager.GetResponseBodySerializer(interfaceId);
            IResponseMessageBody responseMessageBody = null;
            if (retval != null && retval.GetBody() != null)
            {
                responseMessageBody =
                    responseSerializer.Deserialize(new IncomingMessageBody(retval.GetBody().GetRecievedStream()));
            }

            return (IResponseMessage)new ServiceRemotingResponseMessage(header, responseMessageBody);
            */

            return null;
        }

        public void SendOneWay(IRequestMessage requestMessage)
        {
            throw new NotImplementedException();
        }
    }
}

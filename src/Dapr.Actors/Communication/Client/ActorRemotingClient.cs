// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

namespace Dapr.Actors.Communication.Client;

using System.Threading;
using System.Threading.Tasks;

internal class ActorRemotingClient
{
    private readonly ActorMessageSerializersManager serializersManager;
    private readonly IActorMessageBodyFactory remotingMessageBodyFactory = null;
    private readonly IDaprInteractor daprInteractor;

    public ActorRemotingClient(
        IDaprInteractor daprInteractor,
        IActorMessageBodySerializationProvider serializationProvider = null)
    {
        this.daprInteractor = daprInteractor;
        this.serializersManager = IntializeSerializationManager(serializationProvider);
        this.remotingMessageBodyFactory = this.serializersManager.GetSerializationProvider().CreateMessageBodyFactory();
    }

    /// <summary>
    /// Gets a factory for creating the remoting message bodies.
    /// </summary>
    /// <returns>A factory for creating the remoting message bodies.</returns>
    public IActorMessageBodyFactory GetRemotingMessageBodyFactory()
    {
        return this.remotingMessageBodyFactory;
    }

    public async Task<IActorResponseMessage> InvokeAsync(
        IActorRequestMessage remotingRequestMessage,
        CancellationToken cancellationToken)
    {
        return await this.daprInteractor.InvokeActorMethodWithRemotingAsync(this.serializersManager, remotingRequestMessage, cancellationToken);
    }

    private static ActorMessageSerializersManager IntializeSerializationManager(
        IActorMessageBodySerializationProvider serializationProvider)
    {
        // TODO serializer settings
        return new ActorMessageSerializersManager(
            serializationProvider,
            new ActorMessageHeaderSerializer());
    }
}
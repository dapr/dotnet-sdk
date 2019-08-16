// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Communication.Client;

    /// <summary>
    /// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
    /// The proxy object can be used used for client-to-actor and actor-to-actor communication.
    /// </summary>
    public abstract class ActorProxy : IActorProxy
    {
        internal static readonly ActorProxyFactory DefaultProxyFactory = new ActorProxyFactory();
        private ActorCommunicationClient actorCommunicationClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// </summary>
        protected ActorProxy()
        {
        }

        /// <inheritdoc/>
        public ActorId ActorId
        {
            get
            {
                return this.actorCommunicationClient.ActorId;
            }
        }

        /// <inheritdoc/>
        /// <summary>
        /// Gets the <see cref="IActorCommunicationClient"/> interface that this proxy is using to communicate with the actor.
        /// </summary>
        /// <value><see cref="ActorCommunicationClient"/> that this proxy is using to communicate with the actor.</value>
        public IActorCommunicationClient ActorCommunicationClient
        {
            get { return this.actorCommunicationClient; }
        }

        internal IActorMessageBodyFactory ActorMessageBodyFactory { get; set; }

        /// <summary>
        /// Creates a proxy to the actor object that implements an actor interface.
        /// </summary>
        /// <typeparam name="TActorInterface">
        /// The actor interface implemented by the remote actor object.
        /// The returned proxy object will implement this interface.
        /// </typeparam>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        /// <returns>Proxy to the actor object.</returns>
        public static TActorInterface Create<TActorInterface>(ActorId actorId, Type actorType)
            where TActorInterface : IActor
        {
            return DefaultProxyFactory.CreateActorProxy<TActorInterface>(actorId, actorType);
        }

        /// <summary>
        /// Creates a proxy to the actor object that doesnt implement the actor interface.
        /// </summary>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        /// <returns>Proxy to the actor object.</returns>
        public static ActorProxy Create(ActorId actorId, Type actorType)
        {
            return DefaultProxyFactory.CreateActorProxy(actorId, actorType);
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided json payload.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="json">Json payload for actor method.</param>
        /// <returns>Json response form server.</returns>
        public async Task<string> InvokeAsync(string method, string json)
        {
            await Task.CompletedTask;
            return string.Empty;
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided json payload.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="json">Json payload for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Json response form server.</returns>
        public async Task<string> InvokeAsync(string method, string json, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.CompletedTask;
            return string.Empty;
        }

        internal void Initialize(
          ActorCommunicationClient client,
          IActorMessageBodyFactory actorMessageBodyFactory)
        {
            this.actorCommunicationClient = client;
            this.ActorMessageBodyFactory = actorMessageBodyFactory;
        }

        /// <summary>
        /// Invokes the specified method for the actor with provided request.
        /// </summary>
        /// <param name="interfaceId">Interface ID.</param>
        /// <param name="methodId">Method ID.</param>
        /// <param name="methodName">Method Name.</param>
        /// <param name="requestMsgBodyValue">Request Message Body Value.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>A <see cref="Task{TResult}"/> representing the result of the asynchronous operation.</returns>
        protected async Task<IActorMessageBody> InvokeAsync(
            int interfaceId,
            int methodId,
            string methodName,
            IActorMessageBody requestMsgBodyValue,
            CancellationToken cancellationToken)
        {
            var headers = new ActorRequestMessageHeader
            {
                ActorId = this.ActorId,
                ActorType = this.actorCommunicationClient.ActorType.Name,
                InterfaceId = interfaceId,
                MethodId = methodId,
                CallContext = Actors.Helper.GetCallContext(),
                MethodName = methodName,
            };

            var responseMsg = await this.actorCommunicationClient.InvokeAsync(
                new ActorRequestMessage(
                headers,
                requestMsgBodyValue),
                methodName,
                cancellationToken);

            return responseMsg != null ? responseMsg.GetBody()
                   : null;
        }

        /// <summary>
        /// Creates the Remoting request message Body.
        /// </summary>
        /// <param name="interfaceName">Full Name of the service interface for which this call is invoked.</param>
        /// <param name="methodName">Method Name of the service interface for which this call is invoked.</param>
        /// <param name="parameterCount">Number of Parameters in the service interface Method.</param>
        /// <param name="wrappedRequest">Wrapped Request Object.</param>
        /// <returns>A request message body for V2 remoting stack.</returns>
        protected IActorMessageBody CreateRequestMessageBody(
            string interfaceName,
            string methodName,
            int parameterCount,
            object wrappedRequest)
        {
            return this.ActorMessageBodyFactory.CreateMessageBody(interfaceName, methodName, wrappedRequest, parameterCount);
        }

        /// <summary>
        /// This method is used by the generated proxy type and should be used directly. This method converts the Task with object
        /// return value to a Task without the return value for the void method invocation.
        /// </summary>
        /// <param name="task">A task returned from the method that contains null return value.</param>
        /// <returns>A task that represents the asynchronous operation for remote method call without the return value.</returns>
        protected Task ContinueWith(Task<object> task)
        {
            return task;
        }

        /// <summary>
        /// This method is used by the generated proxy type and should be used directly. This method converts the Task with object
        /// return value to a Task without the return value for the void method invocation.
        /// </summary>
        /// <param name="interfaceId">Interface Id for the actor interface.</param>
        /// <param name="methodId">Method Id for the actor method.</param>
        /// <param name="responseBody">Response body.</param>
        /// <returns>Return value of method call as <see cref="object"/>.</returns>
        protected abstract object GetReturnValue(int interfaceId, int methodId, object responseBody);

        /// <summary>
        /// Called by the generated proxy class to get the result from the response body.
        /// </summary>
        /// <typeparam name="TRetval"><see cref="System.Type"/> of the remote method return value.</typeparam>
        /// <param name="interfaceId">InterfaceId of the remoting interface.</param>
        /// <param name="methodId">MethodId of the remoting Method.</param>
        /// <param name="task">A task that represents the asynchronous operation for remote method call.</param>
        /// <returns>A task that represents the asynchronous operation for remote method call.
        /// The value of the TRetval contains the remote method return value. </returns>
        protected async Task<TRetval> ContinueWithResult<TRetval>(
            int interfaceId,
            int methodId,
            Task<IActorMessageBody> task)
        {
            var responseBody = await task;
            var wrappedMessage = responseBody as WrappedMessage;
            if (wrappedMessage != null)
            {
                return (TRetval)this.GetReturnValue(
                    interfaceId,
                    methodId,
                    wrappedMessage.Value);
            }

            return (TRetval)responseBody.Get(typeof(TRetval));
        }

        /// <summary>
        /// This check if we are wrapping remoting message or not.
        /// </summary>
        /// <param name="requestMessage">Remoting Request Message.</param>
        /// <returns>true or false. </returns>
        protected bool CheckIfItsWrappedRequest(IActorMessageBody requestMessage)
        {
            if (requestMessage is WrappedMessage)
            {
                return true;
            }

            return false;
        }
    }
}

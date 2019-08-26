// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Client
{    
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Communication.Client;
    using Newtonsoft.Json;

    /// <summary>
    /// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
    /// The proxy object can be used used for client-to-actor and actor-to-actor communication.
    /// </summary>
    public class ActorProxy : IActorProxy
    {
        internal static readonly ActorProxyFactory DefaultProxyFactory = new ActorProxyFactory();
        private static ActionsHttpInteractor actionsHttpInteractor = new ActionsHttpInteractor();
        private ActorCommunicationClient actorCommunicationClient;
        private string actorType;
        private ActorId actorId;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// </summary>
        protected ActorProxy()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ActorProxy"/> class.
        /// </summary>
        /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
        /// being sent to the actor with this ID.</param>
        /// <param name="actorType">
        /// Type of actor implementation.
        /// </param>
        protected ActorProxy(ActorId actorId, string actorType)
        {
            this.actorType = actorType;
            this.actorId = actorId;
        }

        /// <inheritdoc/>
        public ActorId ActorId
        {
            get
            {
                return this.actorCommunicationClient.ActorId;
            }
        }

        /// <summary>
        /// Gets the <see cref="IActorCommunicationClient"/> interface that this proxy is using to communicate with the actor.
        /// </summary>
        /// <value><see cref="ActorCommunicationClient"/> that this proxy is using to communicate with the actor.</value>
        internal IActorCommunicationClient ActorCommunicationClient
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
        public static TActorInterface Create<TActorInterface>(ActorId actorId, string actorType) 
            where TActorInterface : IActor
        {
            return DefaultProxyFactory.CreateActorProxy<TActorInterface>(actorId, actorType);
        }

        /// <summary>
        /// Creates an Actor Proxy.
        /// </summary>
        /// <param name="actorId">Actor Id.</param>
        /// <param name="actorType">Type of actor.</param>
        /// <returns>Actor proxy to interact with remote actor object.</returns>
        public static ActorProxy Create(ActorId actorId, string actorType)
        {
            return new ActorProxy(actorId, actorType);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument. The argument will be serialized as json.
        /// </summary>
        /// <typeparam name="T">Return type of method.</typeparam>
        /// <param name="method">Actor method name.</param>
        /// <param name="data">Object argument for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public async Task<T> InvokeAsync<T>(string method, object data, CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO: Allow users to provide a custom Serializer.
            var serializer = new JsonSerializer();
            var jsonPayload = JsonConvert.SerializeObject(data);
            var response = await actionsHttpInteractor.InvokeActorMethodWithoutRemotingAsync(this.actorType, this.actorId.ToString(), method, jsonPayload, cancellationToken);

            using (var streamReader = new StreamReader(response))
            {
                using (var reader = new JsonTextReader(streamReader))
                {
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument. The argument will be serialized as json.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="data">Object argument for actor method.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public Task InvokeAsync(string method, object data, CancellationToken cancellationToken = default(CancellationToken))
        {
            // TODO: Allow users to provide a custom Serializer.
            var jsonPayload = JsonConvert.SerializeObject(data);

            return actionsHttpInteractor.InvokeActorMethodWithoutRemotingAsync(this.actorType, this.actorId.ToString(), method, jsonPayload, cancellationToken);
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument.
        /// </summary>
        /// <typeparam name="T">Return type of method.</typeparam>
        /// <param name="method">Actor method name.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public async Task<T> InvokeAsync<T>(string method, CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await actionsHttpInteractor.InvokeActorMethodWithoutRemotingAsync(this.actorType, this.actorId.ToString(), method, null, cancellationToken);
            var serializer = new JsonSerializer();

            using (var streamReader = new StreamReader(response))
            {
                using (var reader = new JsonTextReader(streamReader))
                {
                    return serializer.Deserialize<T>(reader);
                }
            }
        }

        /// <summary>
        /// Invokes the specified method for the actor with argument.
        /// </summary>
        /// <param name="method">Actor method name.</param>
        /// <param name="cancellationToken">Cancellation Token.</param>
        /// <returns>Response form server.</returns>
        public async Task InvokeAsync(string method, CancellationToken cancellationToken = default(CancellationToken))
        {
            await actionsHttpInteractor.InvokeActorMethodWithoutRemotingAsync(this.actorType, this.actorId.ToString(), method, null, cancellationToken);
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
                ActorType = this.actorCommunicationClient.ActorType,
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
        protected virtual object GetReturnValue(int interfaceId, int methodId, object responseBody)
        {
            return null;
        }

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

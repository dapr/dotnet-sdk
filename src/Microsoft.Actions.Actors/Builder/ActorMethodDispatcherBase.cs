// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Builder
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Description;

    /// <summary>
    /// The class is used by actor remoting code generator to generate a type that dispatches requests to actor
    /// object by invoking right method on it.
    /// </summary>
    public abstract class ActorMethodDispatcherBase
    {
        private IReadOnlyDictionary<int, string> methodNameMap;

        /// <summary>
        /// Gets the id of the interface supported by this method dispatcher.
        /// </summary>
        public int InterfaceId { get; private set; }

        /// <summary>
        /// Why we pass IMessageBodyFactory to this function instead of
        /// setting at class level?. Since we cache MethodDispatcher for each interface,
        /// we can't set IMessageBodyFactory at class level.
        /// These can be cases where multiple IMessageBodyFactory implmenetation but single dispatcher class.
        /// This method is used to dispatch request to the specified methodId of the
        /// interface implemented by the remoted object.
        /// </summary>
        /// <param name="objectImplementation">The object impplemented the remoted interface.</param>
        /// <param name="methodId">Id of the method to which to dispatch the request to.</param>
        /// <param name="requestBody">The body of the request object that needs to be dispatched to the object.</param>
        /// <param name="remotingMessageBodyFactory">IMessageBodyFactory implementaion.</param>
        /// <param name="cancellationToken">The cancellation token that will be signaled if this operation is cancelled.</param>
        /// <returns>A task that represents the outstanding asynchronous call to the implementation object.
        /// The return value of the task contains the returned value from the invoked method.</returns>
        public Task<IActorMessageBody> DispatchAsync(
            object objectImplementation,
            int methodId,
            IActorMessageBody requestBody,
            IActorMessageBodyFactory remotingMessageBodyFactory,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var dispatchTask = this.OnDispatchAsync(
                methodId,
                objectImplementation,
                requestBody,
                remotingMessageBodyFactory,
                cancellationToken);

            return dispatchTask;
        }

        /// <summary>
        /// This method is used to dispatch one way messages to the specified methodId of the
        /// interface implemented by the remoted object.
        /// </summary>
        /// <param name="objectImplementation">The object implemented the remoted interface.</param>
        /// <param name="methodId">Id of the method to which to dispatch the request to.</param>
        /// <param name="requestMessageBody">The body of the request object that needs to be dispatched to the remoting implementation.</param>
        public void Dispatch(object objectImplementation, int methodId, IActorMessageBody requestMessageBody)
        {
            this.OnDispatch(methodId, objectImplementation, requestMessageBody);
        }

        /// <summary>
        /// Gets the name of the method that has the specified methodId.
        /// </summary>
        /// <param name="methodId">The id of the method.</param>
        /// <returns>The name of the method corresponding to the specified method id.</returns>
        public string GetMethodName(int methodId)
        {
            if (!this.methodNameMap.TryGetValue(methodId, out var methodName))
            {
                throw new MissingMethodException(string.Format(
                    CultureInfo.CurrentCulture,
                    SR.ErrorMissingMethod,
                    methodId,
                    this.InterfaceId));
            }

            return methodName;
        }

        internal void Initialize(InterfaceDescription description, IReadOnlyDictionary<int, string> methodMap)
        {
            this.SetInterfaceId(description.Id);
            this.SetMethodNameMap(methodMap);
        }

        internal void SetInterfaceId(int interfaceId)
        {
            this.InterfaceId = interfaceId;
        }

        internal void SetMethodNameMap(IReadOnlyDictionary<int, string> methodNameMap)
        {
            this.methodNameMap = methodNameMap;
        }

        /// <summary>
        /// This method is used to create the remoting response from the specified return value.
        /// </summary>
        /// <param name="interfaceName">Interface Name of the remoting Interface.</param>
        /// <param name="methodName">Method Name of the remoting method.</param>
        /// <param name="methodId">MethodId of the remoting method.</param>
        /// <param name="remotingMessageBodyFactory">MessageFactory for the remoting Interface.</param>
        /// <param name="response">Response returned by remoting method.</param>
        /// <returns>Remoting Response.</returns>
        protected IActorMessageBody CreateResponseMessageBody(
            string interfaceName,
            string methodName,
            int methodId,
            IActorMessageBodyFactory remotingMessageBodyFactory,
            object response)
        {
            var msg = remotingMessageBodyFactory.CreateMessageBody(
                interfaceName,
                methodName,
                this.CreateWrappedResponseBody(methodId, response));

            if (!(msg is WrappedMessage))
            {
                msg.Set(response);
            }

            return msg;
        }

        /// <summary>
        /// This method is implemented by the generated method dispatcher to dispatch request to the specified methodId of the
        /// interface implemented by the remoted object.
        /// </summary>
        /// <param name="methodId">Id of the method.</param>
        /// <param name="remotedObject">The remoted object instance.</param>
        /// <param name="requestBody">Request body.</param>
        /// <param name="remotingMessageBodyFactory">Remoting Message Body Factory implementation needed for creating response object.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation.
        /// The result of the task is the return value from the method.
        /// </returns>
        protected abstract Task<IActorMessageBody> OnDispatchAsync(
                    int methodId,
                    object remotedObject,
                    IActorMessageBody requestBody,
                    IActorMessageBodyFactory remotingMessageBodyFactory,
                    CancellationToken cancellationToken);

        /// <summary>
        /// This method is implemented by the generated method dispatcher to dispatch one way messages to the specified methodId of the
        /// interface implemented by the remoted object.
        /// </summary>
        /// <param name="methodId">Id of the method.</param>
        /// <param name="remotedObject">The remoted object instance.</param>
        /// <param name="requestBody">Request body.</param>
        protected abstract void OnDispatch(int methodId, object remotedObject, IActorMessageBody requestBody);

        /// <summary>
        /// Internal - used by Service remoting.
        /// </summary>
        /// <param name="interfaceName">Interface Name of the remoting Interface.</param>
        /// <param name="methodName">Method Name of the remoting method.</param>
        /// <param name="methodId">MethodId of the remoting method.</param>
        /// <param name="remotingMessageBodyFactory">MessageFactory for the remoting Interface.</param>
        /// <param name="task">continuation task.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation.
        /// </returns>
        /// <typeparam name="TRetVal">The response type for the remoting method.</typeparam>
        protected Task<IActorMessageBody> ContinueWithResult<TRetVal>(
            string interfaceName,
            string methodName,
            int methodId,
            IActorMessageBodyFactory remotingMessageBodyFactory,
            Task<TRetVal> task)
        {
            return task.ContinueWith(
                t => this.CreateResponseMessageBody(interfaceName, methodName, methodId, remotingMessageBodyFactory, t.GetAwaiter().GetResult()),
                TaskContinuationOptions.ExecuteSynchronously);
        }

        /// <summary>
        /// Internal - used by remoting.
        /// </summary>
        /// <param name="task">continuation task.</param>
        /// <returns>
        /// A <see cref="System.Threading.Tasks.Task">Task</see> that represents outstanding operation.
        /// </returns>
        protected Task<object> ContinueWith(Task task)
        {
            return task.ContinueWith<object>(
                t =>
                {
                    t.GetAwaiter().GetResult();
                    return null;
                },
                TaskContinuationOptions.ExecuteSynchronously);
        }

        /// Internal - used by remoting
        /// <summary>
        /// This checks if we are wrapping remoting message or not.
        /// </summary>
        /// <param name="requestMessage">Remoting Request Message.</param>
        /// <returns>true or false.</returns>
        protected bool CheckIfItsWrappedRequest(IActorMessageBody requestMessage)
        {
            if (requestMessage is WrappedMessage)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Creates Wrapped Response Object for a method.
        /// </summary>
        /// <param name="methodId">MethodId of the remoting method.</param>
        /// <param name="retVal">Response for a method.</param>
        /// <returns>Wrapped Ressponse object.</returns>
        // Generated By Code-gen
        protected abstract object CreateWrappedResponseBody(
            int methodId,
            object retVal);
    }
}

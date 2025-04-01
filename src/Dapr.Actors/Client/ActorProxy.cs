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

namespace Dapr.Actors.Client;

using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Dapr.Actors.Communication;
using Dapr.Actors.Communication.Client;

/// <summary>
/// Provides the base implementation for the proxy to the remote actor objects implementing <see cref="IActor"/> interfaces.
/// The proxy object can be used for client-to-actor and actor-to-actor communication.
/// </summary>
public class ActorProxy : IActorProxy
{
    /// <summary>
    /// The default factory used to create an actor proxy
    /// </summary>
    public static IActorProxyFactory DefaultProxyFactory { get; } = new ActorProxyFactory();

    private ActorRemotingClient actorRemotingClient;
    private ActorNonRemotingClient actorNonRemotingClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActorProxy"/> class.
    /// This constructor is protected so that it can be used by generated class which derives from ActorProxy when making Remoting calls.
    /// This constructor is also marked as internal so that it can be called by ActorProxyFactory when making non-remoting calls.
    /// </summary>
    protected internal ActorProxy()
    {
    }

    /// <inheritdoc/>
    public ActorId ActorId { get; private set; }

    /// <inheritdoc/>
    public string ActorType { get; private set; }

    internal IActorMessageBodyFactory ActorMessageBodyFactory { get; set; }
    internal JsonSerializerOptions JsonSerializerOptions { get; set; }
    internal string DaprApiToken;

    /// <summary>
    /// Creates a proxy to the actor object that implements an actor interface.
    /// </summary>
    /// <typeparam name="TActorInterface">
    /// The actor interface implemented by the remote actor object.
    /// The returned proxy object will implement this interface.
    /// </typeparam>
    /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
    /// being sent to the actor with this ID.</param>
    /// <param name="actorType">Type of actor implementation.</param>
    /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
    /// <returns>Proxy to the actor object.</returns>
    public static TActorInterface Create<TActorInterface>(ActorId actorId, string actorType, ActorProxyOptions options = null)
        where TActorInterface : IActor
    {
        return DefaultProxyFactory.CreateActorProxy<TActorInterface>(actorId, actorType, options);
    }

    /// <summary>
    /// Creates a proxy to the actor object that implements an actor interface.
    /// </summary>
    /// <param name="actorId">The actor ID of the proxy actor object. Methods called on this proxy will result in requests
    /// being sent to the actor with this ID.</param>
    /// <param name="actorInterfaceType">
    /// The actor interface type implemented by the remote actor object.
    /// The returned proxy object will implement this interface.
    /// </param>
    /// <param name="actorType">Type of actor implementation.</param>
    /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
    /// <returns>Proxy to the actor object.</returns>
    public static object Create(ActorId actorId, Type actorInterfaceType, string actorType, ActorProxyOptions options = null)
    {
        if (!typeof(IActor).IsAssignableFrom(actorInterfaceType))
        {
            throw new ArgumentException("The interface must implement IActor.", nameof(actorInterfaceType));
        }
        return DefaultProxyFactory.CreateActorProxy(actorId, actorInterfaceType, actorType, options);
    }

    /// <summary>
    /// Creates an Actor Proxy for making calls without Remoting.
    /// </summary>
    /// <param name="actorId">Actor Id.</param>
    /// <param name="actorType">Type of actor.</param>
    /// <param name="options">The optional <see cref="ActorProxyOptions" /> to use when creating the actor proxy.</param>
    /// <returns>Actor proxy to interact with remote actor object.</returns>
    public static ActorProxy Create(ActorId actorId, string actorType, ActorProxyOptions options = null)
    {
        return DefaultProxyFactory.Create(actorId, actorType, options);
    }

    /// <summary>
    /// Invokes the specified method for the actor with argument. The argument will be serialized as JSON.
    /// </summary>
    /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
    /// <typeparam name="TResponse">Return type of method.</typeparam>
    /// <param name="method">Actor method name.</param>
    /// <param name="data">Object argument for actor method.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Response form server.</returns>
    public async Task<TResponse> InvokeMethodAsync<TRequest, TResponse>(string method, TRequest data, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync<TRequest>(stream, data, JsonSerializerOptions);
        await stream.FlushAsync();
        var jsonPayload = Encoding.UTF8.GetString(stream.ToArray());
        var response = await this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, jsonPayload, cancellationToken);
        return await JsonSerializer.DeserializeAsync<TResponse>(response, JsonSerializerOptions);
    }

    /// <summary>
    /// Invokes the specified method for the actor with argument. The argument will be serialized as JSON.
    /// </summary>
    /// <typeparam name="TRequest">The data type of the object that will be serialized.</typeparam>
    /// <param name="method">Actor method name.</param>
    /// <param name="data">Object argument for actor method.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Response form server.</returns>
    public async Task InvokeMethodAsync<TRequest>(string method, TRequest data, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync<TRequest>(stream, data, JsonSerializerOptions);
        await stream.FlushAsync();
        var jsonPayload = Encoding.UTF8.GetString(stream.ToArray());
        await this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, jsonPayload, cancellationToken);
    }

    /// <summary>
    /// Invokes the specified method for the actor with argument.
    /// </summary>
    /// <typeparam name="TResponse">Return type of method.</typeparam>
    /// <param name="method">Actor method name.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Response form server.</returns>
    public async Task<TResponse> InvokeMethodAsync<TResponse>(string method, CancellationToken cancellationToken = default)
    {
        var response = await this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, null, cancellationToken);
        return await JsonSerializer.DeserializeAsync<TResponse>(response, JsonSerializerOptions);
    }

    /// <summary>
    /// Invokes the specified method for the actor with argument.
    /// </summary>
    /// <param name="method">Actor method name.</param>
    /// <param name="cancellationToken">Cancellation Token.</param>
    /// <returns>Response form server.</returns>
    public Task InvokeMethodAsync(string method, CancellationToken cancellationToken = default)
    {
        return this.actorNonRemotingClient.InvokeActorMethodWithoutRemotingAsync(this.ActorType, this.ActorId.ToString(), method, null, cancellationToken);
    }

    /// <summary>
    /// Initialize when ActorProxy is created for Remoting.
    /// </summary>
    internal void Initialize(
        ActorRemotingClient client,
        ActorId actorId,
        string actorType,
        ActorProxyOptions options)
    {
        this.actorRemotingClient = client;
        this.ActorId = actorId;
        this.ActorType = actorType;
        this.ActorMessageBodyFactory = client.GetRemotingMessageBodyFactory();
        this.JsonSerializerOptions = options?.JsonSerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web);
        this.DaprApiToken = options?.DaprApiToken;
    }

    /// <summary>
    /// Initialize when ActorProxy is created for non-Remoting calls.
    /// </summary>
    internal void Initialize(
        ActorNonRemotingClient client,
        ActorId actorId,
        string actorType,
        ActorProxyOptions options)
    {
        this.actorNonRemotingClient = client;
        this.ActorId = actorId;
        this.ActorType = actorType;
        this.JsonSerializerOptions = options?.JsonSerializerOptions ?? this.JsonSerializerOptions;
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
    protected async Task<IActorResponseMessageBody> InvokeMethodAsync(
        int interfaceId,
        int methodId,
        string methodName,
        IActorRequestMessageBody requestMsgBodyValue,
        CancellationToken cancellationToken)
    {
        var headers = new ActorRequestMessageHeader
        {
            ActorId = this.ActorId,
            ActorType = this.ActorType,
            InterfaceId = interfaceId,
            MethodId = methodId,
            CallContext = Actors.Helper.GetCallContext(),
            MethodName = methodName,
        };

        var responseMsg = await this.actorRemotingClient.InvokeAsync(
            new ActorRequestMessage(
                headers,
                requestMsgBodyValue),
            cancellationToken);

        return responseMsg?.GetBody();
    }

    /// <summary>
    /// Creates the Actor request message Body.
    /// </summary>
    /// <param name="interfaceName">Full Name of the service interface for which this call is invoked.</param>
    /// <param name="methodName">Method Name of the service interface for which this call is invoked.</param>
    /// <param name="parameterCount">Number of Parameters in the service interface Method.</param>
    /// <param name="wrappedRequest">Wrapped Request Object.</param>
    /// <returns>A request message body.</returns>
    protected IActorRequestMessageBody CreateRequestMessageBody(
        string interfaceName,
        string methodName,
        int parameterCount,
        object wrappedRequest)
    {
        return this.ActorMessageBodyFactory.CreateRequestMessageBody(interfaceName, methodName, parameterCount, wrappedRequest);
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
        return Task.CompletedTask;
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
        Task<IActorResponseMessageBody> task)
    {
        var responseBody = await task;
        if (responseBody is WrappedMessage wrappedMessage)
        {
            var obj = this.GetReturnValue(
                interfaceId,
                methodId,
                wrappedMessage.Value);

            return (TRetval)obj;
        }

        return (TRetval)responseBody.Get(typeof(TRetval));
    }

    /// <summary>
    /// This check if we are wrapping actor message or not.
    /// </summary>
    /// <param name="requestMessageBody">Actor Request Message Body.</param>
    /// <returns>true or false. </returns>
    protected bool CheckIfItsWrappedRequest(IActorRequestMessageBody requestMessageBody)
    {
        if (requestMessageBody is WrappedMessage)
        {
            return true;
        }

        return false;
    }
}
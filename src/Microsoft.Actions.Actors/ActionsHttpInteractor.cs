// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Security.Authentication;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Actions.Actors.Communication;
    using Microsoft.Actions.Actors.Resources;
    using Newtonsoft.Json;

    /// <summary>
    /// Class to interact with actions runtime over http.
    /// </summary>
    internal class ActionsHttpInteractor : IActionsInteractor
    {
        private const string ActionsEndpoint = Constants.ActionsDefaultEndpoint;
        private const string TraceType = "ActionsHttpInteractor";
        private readonly string actionsPort = Constants.ActionsDefaultPort;
        private readonly HttpClientHandler innerHandler;
        private readonly IReadOnlyList<DelegatingHandler> delegateHandlers;
        private readonly ClientSettings clientSettings;
        private HttpClient httpClient = null;
        private bool disposed = false;

        public ActionsHttpInteractor(
            HttpClientHandler innerHandler = null,
            ClientSettings clientSettings = null,
            params DelegatingHandler[] delegateHandlers)
        {
            // Get Actions port from Environment Variable if it has been overridden.
            var actionsPort = Environment.GetEnvironmentVariable(Constants.ActionsPortEnvironmentVariable);
            if (actionsPort != null)
            {
                this.actionsPort = actionsPort;
            }

            this.innerHandler = innerHandler ?? new HttpClientHandler();
            this.delegateHandlers = delegateHandlers;
            this.clientSettings = clientSettings;

            this.httpClient = this.CreateHttpClient();
        }

        public async Task<string> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                };
                return request;
            }

            var response = await this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
            var stringResponse = await response.Content.ReadAsStringAsync();
            return stringResponse;
        }
        
        public async Task SaveStateAsync(string actorType, string actorId, string keyName, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    Content = new StringContent(data),
                };

                return request;
            }

            var response = await this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public async Task RemoveStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                };

                return request;
            }

            var response = await this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public Task SaveStateTransactionallyAsync(string actorType, string actorId, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateRelativeUrlFormat, actorType, actorId);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    Content = new StringContent(data),
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public async Task<IActorResponseMessage> InvokeActorMethodWithRemotingAsync(ActorMessageSerializersManager serializersManager, IActorRequestMessage remotingRequestRequestMessage, CancellationToken cancellationToken = default)
        {
            var requestMessageHeader = remotingRequestRequestMessage.GetHeader();

            var actorId = requestMessageHeader.ActorId.ToString();
            var methodName = requestMessageHeader.MethodName;
            var actorType = requestMessageHeader.ActorType;
            var interfaceId = requestMessageHeader.InterfaceId;

            var serializedHeader = serializersManager.GetHeaderSerializer()
                .SerializeRequestHeader(remotingRequestRequestMessage.GetHeader());

            var msgBodySeriaizer = serializersManager.GetRequestMessageBodySerializer(interfaceId);
            var serializedMsgBody = msgBodySeriaizer.Serialize(remotingRequestRequestMessage.GetBody());

            // Send Request
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                };

                if (serializedMsgBody != null)
                {
                    request.Content = new ByteArrayContent(serializedMsgBody);
                    request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/octet-stream; charset=utf-8");
                }

                request.Headers.Add(Constants.RequestHeaderName, Encoding.UTF8.GetString(serializedHeader, 0, serializedHeader.Length));

                return request;
            }

            var retval = await this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);

            IActorResponseMessageHeader actorResponseMessageHeader = null;
            if (retval != null && retval.Headers != null)
            {
                if (retval.Headers.TryGetValues(Constants.ErrorResponseHeaderName, out IEnumerable<string> headerValues))
                {
                    var header = headerValues.First();

                    // DeSerialize Actor Response Message Header
                    actorResponseMessageHeader =
                        serializersManager.GetHeaderSerializer()
                            .DeserializeResponseHeaders(
                                new MemoryStream(Encoding.ASCII.GetBytes(header)));
                }
            }

            // Get the http response message body content and extract out expected actor response message body
            IActorResponseMessageBody actorResponseMessageBody = null;
            if (retval != null && retval.Content != null)
            {
                var responseMessageBody = await retval.Content.ReadAsStreamAsync();

                // Deserialize Actor Response Message Body
                // Deserialize to RemoteException when there is response header otherwise normal path
                var responseBodySerializer = serializersManager.GetResponseMessageBodySerializer(interfaceId);

                // actorResponseMessageHeader is not null, it means there is remote exception
                if (actorResponseMessageHeader != null)
                {
                    var isDeserialzied =
                            RemoteException.ToException(
                                responseMessageBody,
                                out var remoteMethodException);
                    if (isDeserialzied)
                    {
                        throw new ActorMethodInvocationException(
                            "Remote Actor Method Exception",
                            remoteMethodException,
                            false /* non transient */);
                    }
                    else
                    {
                        throw new ServiceException(remoteMethodException.GetType().FullName, string.Format(
                            CultureInfo.InvariantCulture,
                            SR.ErrorDeserializationFailure,
                            remoteMethodException.ToString()));
                    }
                }

                actorResponseMessageBody = responseBodySerializer.Deserialize(responseMessageBody);
            }

            return new ActorResponseMessage(actorResponseMessageHeader, actorResponseMessageBody);
        }

        public async Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                };

                if (jsonPayload != null)
                {
                    request.Content = new StringContent(jsonPayload);
                    request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                }

                return request;
            }

            var response = await this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
            var byteArray = await response.Content.ReadAsStreamAsync();
            return byteArray;
        }

        public Task RegisterReminderAsync(string actorType, string actorId, string reminderName, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    Content = new StringContent(data, Encoding.UTF8),
                };

                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public Task UnregisterReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public Task RegisterTimerAsync(string actorType, string actorId, string timerName, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    Content = new StringContent(data, Encoding.UTF8),
                };

                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        public Task UnregisterTimerAsync(string actorType, string actorId, string timerName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.Timers, actorType, actorId, timerName);
            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP get request to Actions.
        /// </summary>
        /// <param name="requestFunc">Func to create HttpRequest to send.</param>
        /// <param name="relativeUri">The relative URI.</param>
        /// <param name="requestId">Request Id for corelation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response.</returns>
        internal async Task<HttpResponseMessage> SendAsync(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            string requestId,
            CancellationToken cancellationToken)
        {
            return await this.SendAsyncHandleUnsuccessfulResponse(requestFunc, relativeUri, requestId, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP get request to Actions and returns the result as raw json.
        /// </summary>
        /// <param name="requestFunc">Func to create HttpRequest to send.</param>
        /// <param name="relativeUri">The relative URI.</param>
        /// <param name="requestId">Request Id for corelation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response as string.</returns>
        internal async Task<string> SendAsyncGetResponseAsRawJson(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            string requestId,
            CancellationToken cancellationToken)
        {
            var response = await this.SendAsyncHandleUnsuccessfulResponse(requestFunc, relativeUri, requestId, cancellationToken);
            var retValue = default(string);

            if (response != null && response.Content != null)
            {
                retValue = await response.Content.ReadAsStringAsync();
            }

            return retValue;
        }

        /// <summary>
        /// Disposes resources.
        /// </summary>
        /// <param name="disposing">False values indicates the method is being called by the runtime, true value indicates the method is called by the user code.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    this.httpClient.Dispose();
                    this.httpClient = null;
                }

                this.disposed = true;
            }
        }

        private static ActorMessageSerializersManager IntializeSerializationManager(
           IActorMessageBodySerializationProvider serializationProvider)
        {
            // TODO serializer settings
            return new ActorMessageSerializersManager(
                serializationProvider,
                new ActorMessageHeaderSerializer());
        }

        /// <summary>
        /// Sends an HTTP get request to cluster http gateway.
        /// </summary>
        /// <param name="requestFunc">Func to create HttpRequest to send.</param>
        /// <param name="relativeUri">The relative URI.</param>
        /// <param name="requestId">Request Id for corelation.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response.</returns>
        private async Task<HttpResponseMessage> SendAsyncHandleUnsuccessfulResponse(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            string requestId,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage FinalRequestFunc()
            {
                var request = requestFunc.Invoke();
                request.RequestUri = new Uri($"http://{ActionsEndpoint}:{this.actionsPort}/{relativeUri}");

                // Add correlation IDs.
                request.Headers.Add(Constants.RequestIdHeaderName, this.GetClientRequestIdWithCorrelation(requestId));
                return request;
            }

            var response = await this.SendAsyncHandleSecurityExceptions(FinalRequestFunc, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return response;
            }

            // TODO Log Unsuccessful Response.

            // Try to get Error Information if present in response body.
            if (response.Content != null)
            {
                ActionsError error = null;

                try
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    if (contentStream.Length != 0)
                    {
                        using var streamReader = new StreamReader(contentStream);
                        var json = await streamReader.ReadToEndAsync();
                        error = JsonConvert.DeserializeObject<ActionsError>(json);
                    }
                }
                catch (Exception ex)
                {
                    throw new ActionsException(string.Format("ServerErrorNoMeaningFulResponse", response.StatusCode), ex);
                }

                if (error != null)
                {
                    throw new ActionsException(error.Message, error.ErrorCode ?? ActionsErrorCodes.UNKNOWN, false);
                }
                else
                {
                    // Handle NotFound 404, without any ErrorCode.
                    if (response.StatusCode.Equals(HttpStatusCode.NotFound))
                    {
                        throw new ActionsException("ErrorMessageHTTP404", ActionsErrorCodes.ERR_DOES_NOT_EXIST, false);
                    }
                }
            }

            // Couldn't determine Error information from response., throw exception with status code.
            throw new ActionsException(string.Format("ServerErrorNoMeaningFulResponse", response.StatusCode));
        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation using HttpClient and handles security exceptions.
        /// If UserCode to Actions calls are over https in future, this method will handle refreshing security.
        /// </summary>
        /// <param name="requestFunc">Delegate to get HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        private async Task<HttpResponseMessage> SendAsyncHandleSecurityExceptions(
            Func<HttpRequestMessage> requestFunc,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response = null;

            try
            {
                // Get the request using the Func as same request cannot be resent when retries are implemented.
                var request = requestFunc.Invoke();
                response = await this.httpClient.SendAsync(request, cancellationToken);
            }
            catch (AuthenticationException ex)
            {
                ActorTrace.Instance.WriteError(TraceType, ex.ToString());
                throw;
            }
            catch (HttpRequestException ex)
            {
                ActorTrace.Instance.WriteError(TraceType, ex.ToString());
                throw;
            }

            if (!response.IsSuccessStatusCode)
            {
                // RefreshSecurity Settings and try again,
                if (response.StatusCode == HttpStatusCode.Forbidden)
                {
                    // TODO Log
                    throw new AuthenticationException("Invalid client credentials");
                }
                else
                {
                    return response;
                }
            }
            else
            {
                return response;
            }
        }

        private string GetClientRequestIdWithCorrelation(string requestId)
        {
            // TODO: Add external correlations for tracing.
            return requestId;
        }

        private HttpClient CreateHttpClient()
        {
            // Chain Delegating Handlers.
            HttpMessageHandler pipeline = this.innerHandler;
            if (this.delegateHandlers != null)
            {
                for (var i = this.delegateHandlers.Count - 1; i >= 0; i--)
                {
                    var handler = this.delegateHandlers[i];
                    handler.InnerHandler = pipeline;
                    pipeline = handler;
                }
            }

            var httpClientInstance = new HttpClient(pipeline, true);
            if (this.clientSettings?.ClientTimeout != null)
            {
                httpClientInstance.Timeout = (TimeSpan)this.clientSettings.ClientTimeout;
            }

            return httpClientInstance;
        }
    }
}

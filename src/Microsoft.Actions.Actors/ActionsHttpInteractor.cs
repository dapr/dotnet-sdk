﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Actions.Actors.Runtime
{
    using System;
    using System.Collections.Generic;
    using System.IO;
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
        private const string ActionsEndpoint = "localhost";
        private string actionsPort = "3550";
        private HttpClient httpClient = null;
        private HttpClientHandler innerHandler;
        private IReadOnlyList<DelegatingHandler> delegateHandlers;
        private HttpClientSettings clientSettings;
        private bool disposed = false;

        public ActionsHttpInteractor(
            HttpClientHandler innerHandler = null,
            HttpClientSettings clientSettings = null,
            params DelegatingHandler[] delegateHandlers)
        {
            // Get Actions port from Environment Variable if it has been overridden.
            var actionsPort = Environment.GetEnvironmentVariable(Constants.ActionsPortEnvironmentVariable);
            if (actionsPort != null)
            {
                this.actionsPort = actionsPort;
            }

            this.innerHandler = innerHandler == null ? new HttpClientHandler() : innerHandler;
            this.delegateHandlers = delegateHandlers;
            this.clientSettings = clientSettings;

            this.httpClient = this.CreateHttpClient();
        }

        public Task SaveStateAsync(ActorId actorId, IReadOnlyCollection<ActorStateChange> stateChanges, CancellationToken cancellationToken = default(CancellationToken))
        {
            var url = Constants.ActorStateManagementRelativeUrl;
            var requestId = Guid.NewGuid().ToString();

            // TODO: create the content as serialized state info expected by Actions runtime.
            string content = string.Empty;

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    Content = new StringContent(content, Encoding.UTF8),
                };
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                return request;
            }

            return this.SendAsync(RequestFunc, url, requestId, cancellationToken);
        }

        public Task<string> GetStateAsync(ActorId actorId, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotImplementedException();
        }

        public Task<object> InvokeActorMethod(string actorId, string actorType, string methodName, byte[] messageHeader, byte[] messageBody, CancellationToken cancellationToken = default(CancellationToken))
        {
            var relativeUrl = $"{Constants.ActorRequestRelativeUrl}/{actorType}/{actorId}/{methodName}";

            var requestId = Guid.NewGuid().ToString();

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Post,
                    Content = new ByteArrayContent(messageBody),
                };

                request.Headers.Add(Constants.RequestHeaderName, Encoding.UTF8.GetString(messageHeader, 0, messageHeader.Length));
                request.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse("application/json; charset=utf-8");
                return request;
            }

            return Task.FromResult((object)this.SendAsync(RequestFunc, relativeUrl, requestId, cancellationToken));
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
                        using (var streamReader = new StreamReader(contentStream))
                        {
                            var json = await streamReader.ReadToEndAsync();
                            error = JsonConvert.DeserializeObject<ActionsError>(json);
                        }
                    }
                }
                catch (JsonReaderException ex)
                {
                    throw new ActionsException(string.Format(SR.ServerErrorNoMeaningFulResponse, response.StatusCode), ex);
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
                        throw new ActionsException(SR.ErrorMessageHTTP404, ActionsErrorCodes.ACTIONS_E_DOES_NOT_EXIST, false);
                    }
                }
            }

            // Couldn't determine Error information from response., throw exception with status code.
            throw new ActionsException(string.Format(SR.ServerErrorNoMeaningFulResponse, response.StatusCode));
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
                // TODO Log.
                Console.WriteLine(ex.ToString());
                throw;
            }
            catch (HttpRequestException ex)
            {
                // TODO Log.
                Console.WriteLine(ex.ToString());
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

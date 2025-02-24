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

namespace Dapr.Actors
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
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Actors.Communication;
    using Dapr.Actors.Resources;
    using System.Xml;

    /// <summary>
    /// Class to interact with Dapr runtime over http.
    /// </summary>
    internal class DaprHttpInteractor : IDaprInteractor
    {
        private readonly JsonSerializerOptions jsonSerializerOptions = JsonSerializerDefaults.Web;
        private readonly string httpEndpoint;
        private readonly static HttpMessageHandler defaultHandler = new HttpClientHandler();
        private readonly HttpMessageHandler handler;
        private HttpClient httpClient;
        private bool disposed;
        private string daprApiToken;

        private const string EXCEPTION_HEADER_TAG = "b:KeyValueOfstringbase64Binary";

        public DaprHttpInteractor(
            HttpMessageHandler clientHandler,
            string httpEndpoint,
            string apiToken,
            TimeSpan? requestTimeout)
        {
            this.handler = clientHandler ?? defaultHandler;
            this.httpEndpoint = httpEndpoint;
            this.daprApiToken = apiToken;
            this.httpClient = this.CreateHttpClient();
            this.httpClient.Timeout = requestTimeout ?? this.httpClient.Timeout;
        }

        public async Task<ActorStateResponse<string>> GetStateAsync(string actorType, string actorId, string keyName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateKeyRelativeUrlFormat, actorType, actorId, keyName);

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                };
                return request;
            }

            using var response = await this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
            var stringResponse = await response.Content.ReadAsStringAsync();

            DateTimeOffset? ttlExpireTime = null;
            if (response.Headers.TryGetValues(Constants.TTLResponseHeaderName, out IEnumerable<string> headerValues))
            {
                var ttlExpireTimeString = headerValues.First();
                if (!string.IsNullOrEmpty(ttlExpireTimeString))
                {
                    ttlExpireTime = DateTime.Parse(ttlExpireTimeString, CultureInfo.InvariantCulture);
                }
            }

            return new ActorStateResponse<string>(stringResponse, ttlExpireTime);
        }

        public Task SaveStateTransactionallyAsync(string actorType, string actorId, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorStateRelativeUrlFormat, actorType, actorId);

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Put,
                    Content = new StringContent(data),
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
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

            var msgBodySeriaizer = serializersManager.GetRequestMessageBodySerializer(interfaceId, methodName);
            var serializedMsgBody = msgBodySeriaizer.Serialize(remotingRequestRequestMessage.GetBody());

            // Send Request
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);

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

                var reentrancyId = ActorReentrancyContextAccessor.ReentrancyContext;
                if (reentrancyId != null)
                {
                    request.Headers.Add(Constants.ReentrancyRequestHeaderName, reentrancyId);
                }

                return request;
            }

            var retval = await this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
            var header = "";
            IActorResponseMessageHeader actorResponseMessageHeader = null;
            if (retval != null && retval.Headers != null)
            {
                if (retval.Headers.TryGetValues(Constants.ErrorResponseHeaderName, out IEnumerable<string> headerValues))
                {
                    header = headerValues.First();
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
                // Deserialize to ActorInvokeException when there is response header otherwise normal path
                var responseBodySerializer = serializersManager.GetResponseMessageBodySerializer(interfaceId, methodName);

                // actorResponseMessageHeader is not null, it means there is remote exception
                if (actorResponseMessageHeader != null)
                {
                    var isDeserialized =
                            ActorInvokeException.ToException(
                                responseMessageBody,
                                out var remoteMethodException);
                    if (isDeserialized)
                    {
                        var exceptionDetails = GetExceptionDetails(header.ToString());
                        throw new ActorMethodInvocationException(
                            "Remote Actor Method Exception,  DETAILS: " + exceptionDetails,
                            remoteMethodException,
                            false /* non transient */);
                    }
                    else
                    {
                        throw new ActorInvokeException(remoteMethodException.GetType().FullName, string.Format(
                            CultureInfo.InvariantCulture,
                            SR.ErrorDeserializationFailure,
                            remoteMethodException.ToString()));
                    }
                }

                actorResponseMessageBody = await responseBodySerializer.DeserializeAsync(responseMessageBody);
            }

            return new ActorResponseMessage(actorResponseMessageHeader, actorResponseMessageBody);
        }

        private string GetExceptionDetails(string header) {
            XmlDocument xmlHeader = new XmlDocument();
            xmlHeader.LoadXml(header);
            XmlNodeList exceptionValueXML = xmlHeader.GetElementsByTagName(EXCEPTION_HEADER_TAG);
            string exceptionDetails = "";
            if (exceptionValueXML != null && exceptionValueXML.Item(1) != null)
            {
                exceptionDetails = exceptionValueXML.Item(1).LastChild.InnerText;
            }
            var base64EncodedBytes = System.Convert.FromBase64String(exceptionDetails);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public async Task<Stream> InvokeActorMethodWithoutRemotingAsync(string actorType, string actorId, string methodName, string jsonPayload, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorMethodRelativeUrlFormat, actorType, actorId, methodName);

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

                var reentrancyId = ActorReentrancyContextAccessor.ReentrancyContext;
                if (reentrancyId != null)
                {
                    request.Headers.Add(Constants.ReentrancyRequestHeaderName, reentrancyId);
                }

                return request;
            }

            var response = await this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
            var stream = await response.Content.ReadAsStreamAsync();
            return stream;
        }

        public Task RegisterReminderAsync(string actorType, string actorId, string reminderName, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

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

            return this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
        }

        public async Task<HttpResponseMessage> GetReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Get,
                };    
                return request;
            }

            return await this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
        }

        public Task UnregisterReminderAsync(string actorType, string actorId, string reminderName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorReminderRelativeUrlFormat, actorType, actorId, reminderName);

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
        }

        public Task RegisterTimerAsync(string actorType, string actorId, string timerName, string data, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

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

            return this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
        }

        public Task UnregisterTimerAsync(string actorType, string actorId, string timerName, CancellationToken cancellationToken = default)
        {
            var relativeUrl = string.Format(CultureInfo.InvariantCulture, Constants.ActorTimerRelativeUrlFormat, actorType, actorId, timerName);

            HttpRequestMessage RequestFunc()
            {
                var request = new HttpRequestMessage()
                {
                    Method = HttpMethod.Delete,
                };

                return request;
            }

            return this.SendAsync(RequestFunc, relativeUrl, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP get request to Dapr.
        /// </summary>
        /// <param name="requestFunc">Func to create HttpRequest to send.</param>
        /// <param name="relativeUri">The relative URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response.</returns>
        internal async Task<HttpResponseMessage> SendAsync(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            CancellationToken cancellationToken)
        {
            return await this.SendAsyncHandleUnsuccessfulResponse(requestFunc, relativeUri, cancellationToken);
        }

        /// <summary>
        /// Sends an HTTP get request to Dapr and returns the result as raw JSON.
        /// </summary>
        /// <param name="requestFunc">Func to create HttpRequest to send.</param>
        /// <param name="relativeUri">The relative URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response as string.</returns>
        internal async Task<string> SendAsyncGetResponseAsRawJson(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            CancellationToken cancellationToken)
        {
            using var response = await this.SendAsyncHandleUnsuccessfulResponse(requestFunc, relativeUri, cancellationToken);
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
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The payload of the GET response.</returns>
        private async Task<HttpResponseMessage> SendAsyncHandleUnsuccessfulResponse(
            Func<HttpRequestMessage> requestFunc,
            string relativeUri,
            CancellationToken cancellationToken)
        {
            HttpRequestMessage FinalRequestFunc()
            {
                var request = requestFunc.Invoke();
                request.RequestUri = new Uri($"{this.httpEndpoint}/{relativeUri}");
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
                DaprError error = null;

                try
                {
                    var contentStream = await response.Content.ReadAsStreamAsync();
                    if (contentStream.Length != 0)
                    {
                        error = await JsonSerializer.DeserializeAsync<DaprError>(contentStream, jsonSerializerOptions);
                    }
                }
                catch (Exception ex)
                {
                    throw new DaprApiException(string.Format("ServerErrorNoMeaningFulResponse", response.StatusCode), ex);
                }

                if (error != null)
                {
                    throw new DaprApiException(error.Message, error.ErrorCode, false);
                }
                else
                {
                    // Handle NotFound 404, without any ErrorCode.
                    if (response.StatusCode.Equals(HttpStatusCode.NotFound))
                    {
                        throw new DaprApiException("ErrorMessageHTTP404", Constants.ErrorDoesNotExist, false);
                    }
                }
            }

            // Couldn't determine Error information from response., throw exception with status code.
            throw new DaprApiException(string.Format("ServerErrorNoMeaningFulResponse", response.StatusCode));
        }

        /// <summary>
        /// Send an HTTP request as an asynchronous operation using HttpClient and handles security exceptions.
        /// If UserCode to Dapr calls are over https in future, this method will handle refreshing security.
        /// </summary>
        /// <param name="requestFunc">Delegate to get HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <returns>The task object representing the asynchronous operation.</returns>
        private async Task<HttpResponseMessage> SendAsyncHandleSecurityExceptions(
            Func<HttpRequestMessage> requestFunc,
            CancellationToken cancellationToken)
        {
            HttpResponseMessage response;

            // Get the request using the Func as same request cannot be resent when retries are implemented.
            using var request = requestFunc.Invoke();

            // add token for dapr api token based authentication
            this.AddDaprApiTokenHeader(request);

            response = await this.httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // RefreshSecurity Settings and try again,
                if (response.StatusCode == HttpStatusCode.Forbidden ||
                    response.StatusCode == HttpStatusCode.Unauthorized)
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

        private HttpClient CreateHttpClient()
        {
            return new HttpClient(this.handler, false);
        }

        private void AddDaprApiTokenHeader(HttpRequestMessage request)
        {
            if (!string.IsNullOrWhiteSpace(this.daprApiToken))
            {
                request.Headers.Add("dapr-api-token", this.daprApiToken);
                return;
            }
        }
    }
}

﻿// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Dapr.Client.Autogen.Grpc.v1;
    using Dapr.Client.Http;
    using Google.Protobuf;
    using Google.Protobuf.WellKnownTypes;
    using Grpc.Core;
    using Grpc.Net.Client;
    using Autogenerated = Dapr.Client.Autogen.Grpc.v1;

    /// <summary>
    /// A client for interacting with the Dapr endpoints.
    /// </summary>
    internal class DaprClientGrpc : DaprClient
    {
        private readonly Autogenerated.Dapr.DaprClient client;
        private readonly JsonSerializerOptions jsonSerializerOptions;

        /// <summary>
        /// Initializes a new instance of the <see cref="DaprClientGrpc"/> class.
        /// </summary>
        /// <param name="channel">gRPC channel to create gRPC clients.</param>
        /// <param name="jsonSerializerOptions">Json serialization options.</param>
        internal DaprClientGrpc(GrpcChannel channel, JsonSerializerOptions jsonSerializerOptions = null)
        {
            this.jsonSerializerOptions = jsonSerializerOptions;
            this.client = new Autogenerated.Dapr.DaprClient(channel);
        }

        #region Publish Apis
        /// <inheritdoc/>
        public override Task PublishEventAsync<TData>(string topicName, TData data, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            ArgumentVerifier.ThrowIfNull(data, nameof(data));
            return MakePublishRequest(topicName, data, cancellationToken);
        }

        /// <inheritdoc/>
        public override Task PublishEventAsync(string topicName, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(topicName, nameof(topicName));
            return MakePublishRequest(topicName, string.Empty, cancellationToken);
        }

        private async Task MakePublishRequest<TContent>(string topicName, TContent content, CancellationToken cancellationToken)
        {
            // Create PublishEventEnvelope
            var envelope = new Autogenerated.PublishEventRequest()
            {
                Topic = topicName,
            };

            if (content != null)
            {
                envelope.Data = ConvertToByteStringAsync(content, this.jsonSerializerOptions);
            }

            await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.PublishEventAsync(envelope, options);
                },
                cancellationToken);
        }
        #endregion

        #region InvokeBinding Apis

        /// <inheritdoc/>
        public override async Task InvokeBindingAsync<TRequest>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            _ = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<TResponse> InvokeBindingAsync<TRequest, TResponse>(
            string name,
            string operation,
            TRequest data,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(name, nameof(name));
            ArgumentVerifier.ThrowIfNullOrEmpty(operation, nameof(operation));

            InvokeBindingResponse response = await MakeInvokeBindingRequestAsync(name, operation, data, metadata, cancellationToken);
            return ConvertFromInvokeBindingResponse<TResponse>(response, this.jsonSerializerOptions);
        }

        private static T ConvertFromInvokeBindingResponse<T>(InvokeBindingResponse response, JsonSerializerOptions options = null)
        {
            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<T>(responseData, options);
        }

        private async Task<InvokeBindingResponse> MakeInvokeBindingRequestAsync<TContent>(
           string name,
           string operation,
           TContent data,
           Dictionary<string, string> metadata = default,
           CancellationToken cancellationToken = default)
        {          
            var envelope = new Autogenerated.InvokeBindingRequest()
            {
                Name = name,
                Operation = operation
            };
            
            if (data != null)
            {
                envelope.Data = ConvertToByteStringAsync(data, this.jsonSerializerOptions);
            }

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            return await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.InvokeBindingAsync(envelope, options);
                },
                cancellationToken);
        }
        #endregion

        #region InvokeMethod Apis
        public override async Task InvokeMethodAsync(
           string appId,
           string methodName,
           Http.HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            _ = await this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
        }

        public override async Task InvokeMethodAsync<TRequest>(
           string appId,
           string methodName,
           TRequest data,
           Http.HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Any serializedData = null;
            if (data != null)
            {
                serializedData = ConvertToAnyAsync(data, this.jsonSerializerOptions);
            }

            _ = await this.MakeInvokeRequestAsync(appId, methodName, serializedData, httpExtension, cancellationToken);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TResponse>(
           string appId,
           string methodName,
           Http.HTTPExtension httpExtension = default,
           CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            var response = await this.MakeInvokeRequestAsync(appId, methodName, null, httpExtension, cancellationToken);
            if (response.Data.Value.IsEmpty)
            {
                return default;
            }

            return ConvertFromInvokeResponse<TResponse>(response, this.jsonSerializerOptions);
        }

        public override async ValueTask<TResponse> InvokeMethodAsync<TRequest, TResponse>(
            string appId,
            string methodName,
            TRequest data,
            Http.HTTPExtension httpExtension = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(appId, nameof(appId));
            ArgumentVerifier.ThrowIfNullOrEmpty(methodName, nameof(methodName));

            Any serializedData = null;
            if (data != null)
            {
                serializedData = ConvertToAnyAsync(data, this.jsonSerializerOptions);
            }

            var response = await this.MakeInvokeRequestAsync(appId, methodName, serializedData, httpExtension, cancellationToken);
            if (response.Data.Value.IsEmpty)
            {
                return default;
            }

            return ConvertFromInvokeResponse<TResponse>(response, this.jsonSerializerOptions);
        }

        private async Task<InvokeResponse> MakeInvokeRequestAsync(
            string appId,
            string methodName,
            Any data,
            Http.HTTPExtension httpExtension,
            CancellationToken cancellationToken = default)
        {
            var protoHTTPExtension = new Autogenerated.HTTPExtension();
            var contentType = "";

            if (httpExtension != null)
            {
                protoHTTPExtension.Verb = ConvertHTTPVerb(httpExtension.Verb);

                if (httpExtension.QueryString != null)
                {
                    foreach (var kv in httpExtension.QueryString)
                    {
                        protoHTTPExtension.Querystring.Add(kv.Key, kv.Value);
                    }
                }

                contentType = httpExtension.ContentType ?? Constants.ContentTypeApplicationJson;
            }
            else
            {
                protoHTTPExtension.Verb = Autogenerated.HTTPExtension.Types.Verb.Post;
                contentType = Constants.ContentTypeApplicationJson;
            }

            var invokeRequest = new InvokeRequest()
            {
                Method = methodName,
                Data = data,
                ContentType = contentType,
                HttpExtension = protoHTTPExtension
            };

            var request = new Autogenerated.InvokeServiceRequest()
            {

                Id = appId,
                Message = invokeRequest,
            };

            return await this.MakeGrpcCallHandleError(
                 (options) =>
                 {
                     return client.InvokeServiceAsync(request, options);
                 },
                 cancellationToken);
        }
        #endregion

        #region State Apis
        /// <inheritdoc/>
        public override async ValueTask<TValue> GetStateAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.GetStateAsync(getStateEnvelope, options);
                },
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return default;
            }

            var responseData = response.Data.ToStringUtf8();
            return JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
        }

        /// <inheritdoc/>
        public override async ValueTask<(TValue value, string etag)> GetStateAndETagAsync<TValue>(string storeName, string key, ConsistencyMode? consistencyMode = default, CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var getStateEnvelope = new GetStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (consistencyMode != null)
            {
                getStateEnvelope.Consistency = GetStateConsistencyForConsistencyMode(consistencyMode.Value);
            }

            var response = await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.GetStateAsync(getStateEnvelope, options);
                },
                cancellationToken);

            if (response.Data.IsEmpty)
            {
                return (default(TValue), response.Etag);
            }

            var responseData = response.Data.ToStringUtf8();
            var deserialized = JsonSerializer.Deserialize<TValue>(responseData, this.jsonSerializerOptions);
            return (deserialized, response.Etag);
        }

        /// <inheritdoc/>
        public override async Task SaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeSaveStateCallAsync<TValue>(
                storeName,
                key,
                value,
                etag: null,
                stateOptions,
                metadata,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TrySaveStateAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeSaveStateCallAsync<TValue>(storeName, key, value, etag, stateOptions, metadata, cancellationToken);
                return true;
            }
            catch (RpcException)
            { }

            return false;
        }

        internal async ValueTask MakeSaveStateCallAsync<TValue>(
            string storeName,
            string key,
            TValue value,
            string etag = default,
            StateOptions stateOptions = default,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            // Create PublishEventEnvelope
            var saveStateEnvelope = new Autogenerated.SaveStateRequest()
            {
                StoreName = storeName,
            };

            var stateItem = new Autogenerated.StateItem()
            {
                Key = key,
            };

            if (metadata != null)
            {
                stateItem.Metadata.Add(metadata);
            }

            if (etag != null)
            {
                stateItem.Etag = etag;
            }

            if (stateOptions != null)
            {
                stateItem.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            if (value != null)
            {
                stateItem.Value = ConvertToByteStringAsync(value, this.jsonSerializerOptions);
            }

            saveStateEnvelope.States.Add(stateItem);

            await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.SaveStateAsync(saveStateEnvelope, options);
                },
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async Task DeleteStateAsync(
            string storeName,
            string key,
            StateOptions stateOptions = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            await this.MakeDeleteStateCallsync(
                storeName,
                key,
                etag: null,
                stateOptions,
                cancellationToken);
        }

        /// <inheritdoc/>
        public override async ValueTask<bool> TryDeleteStateAsync(
            string storeName,
            string key,
            string etag = default,
            StateOptions stateOptions = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            try
            {
                await this.MakeDeleteStateCallsync(storeName, key, etag, stateOptions, cancellationToken);
                return true;
            }
            catch (Exception)
            {
            }

            return false;
        }

        private async ValueTask MakeDeleteStateCallsync(
           string storeName,
           string key,
           string etag = default,
           StateOptions stateOptions = default,
           CancellationToken cancellationToken = default)
        {
            var deleteStateEnvelope = new DeleteStateRequest()
            {
                StoreName = storeName,
                Key = key,
            };

            if (etag != null)
            {
                deleteStateEnvelope.Etag = etag;
            }

            if (stateOptions != null)
            {
                deleteStateEnvelope.Options = ToAutoGeneratedStateOptions(stateOptions);
            }

            await this.MakeGrpcCallHandleError(
                (options) =>
                {
                    return client.DeleteStateAsync(deleteStateEnvelope, options);
                },
                cancellationToken);
        }
        #endregion

        #region Secret Apis
        /// <inheritdoc/>
        public async override ValueTask<Dictionary<string, string>> GetSecretAsync(
            string storeName,
            string key,
            Dictionary<string, string> metadata = default,
            CancellationToken cancellationToken = default)
        {
            ArgumentVerifier.ThrowIfNullOrEmpty(storeName, nameof(storeName));
            ArgumentVerifier.ThrowIfNullOrEmpty(key, nameof(key));

            var envelope = new Autogenerated.GetSecretRequest()
            {
                StoreName = storeName,
                Key = key
            };

            if (metadata != null)
            {
                envelope.Metadata.Add(metadata);
            }

            var response = await this.MakeGrpcCallHandleError(
                 (options) =>
                 {
                     return client.GetSecretAsync(envelope, options);
                 },
                 cancellationToken);

            return response.Data.ToDictionary(kv => kv.Key, kv => kv.Value);
        }
        #endregion

        #region Helper Methods

        /// <summary>
        /// Makes Grpc call using the cancellationToken and handles Errors.
        /// All common exception handling logic will reside here.
        /// </summary>
        /// <typeparam name="TResponse"></typeparam>
        /// <param name="callFunc"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private async Task<TResponse> MakeGrpcCallHandleError<TResponse>(Func<CallOptions, AsyncUnaryCall<TResponse>> callFunc, CancellationToken cancellationToken = default)
        {
            var callOptions = new CallOptions(cancellationToken: cancellationToken);

            // Common Exception Handling logic can be added here for all calls.            
            return await callFunc.Invoke(callOptions);
        }

        private Autogenerated.StateOptions ToAutoGeneratedStateOptions(StateOptions stateOptions)
        {
            var stateRequestOptions = new Autogenerated.StateOptions();

            if (stateOptions.Consistency != null)
            {
                stateRequestOptions.Consistency = GetStateConsistencyForConsistencyMode(stateOptions.Consistency.Value);
            }

            if (stateOptions.Concurrency != null)
            {
                stateRequestOptions.Concurrency = GetStateConcurrencyForConcurrencyMode(stateOptions.Concurrency.Value);
            }

            if (stateOptions.RetryOptions != null)
            {
                var retryPolicy = new Autogenerated.StateRetryPolicy();
                if (stateOptions.RetryOptions.RetryMode != null)
                {
                    retryPolicy.Pattern = GetRetryPatternForRetryMode(stateOptions.RetryOptions.RetryMode.Value);
                }

                if (stateOptions.RetryOptions.RetryInterval != null)
                {
                    retryPolicy.Interval = Duration.FromTimeSpan(stateOptions.RetryOptions.RetryInterval.Value);
                }

                if (stateOptions.RetryOptions.RetryThreshold != null)
                {
                    retryPolicy.Threshold = stateOptions.RetryOptions.RetryThreshold.Value;
                }

                stateRequestOptions.RetryPolicy = retryPolicy;
            }

            return stateRequestOptions;
        }

        private static Any ConvertToAnyAsync<T>(T data, JsonSerializerOptions options = null)
        {
            var any = new Any();

            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);
                any.Value = ByteString.CopyFrom(bytes);
            }

            return any;
        }

        private static ByteString ConvertToByteStringAsync<T>(T data, JsonSerializerOptions options = null)
        {
            if (data != null)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(data, options);                                
                return ByteString.CopyFrom(bytes);
            }

            return ByteString.Empty;
        }

        private static T ConvertFromInvokeResponse<T>(InvokeResponse response, JsonSerializerOptions options = null)
        {
            var responseData = response.Data.Value.ToStringUtf8();
            return JsonSerializer.Deserialize<T>(responseData, options);
        }

        private static Autogenerated.HTTPExtension.Types.Verb ConvertHTTPVerb(HTTPVerb verb)
        {
            return verb switch
            {
                HTTPVerb.Get => Autogenerated.HTTPExtension.Types.Verb.Get,
                HTTPVerb.Head => Autogenerated.HTTPExtension.Types.Verb.Head,
                HTTPVerb.Post => Autogenerated.HTTPExtension.Types.Verb.Post,
                HTTPVerb.Put => Autogenerated.HTTPExtension.Types.Verb.Put,
                HTTPVerb.Delete => Autogenerated.HTTPExtension.Types.Verb.Delete,
                HTTPVerb.Connect => Autogenerated.HTTPExtension.Types.Verb.Connect,
                HTTPVerb.Options => Autogenerated.HTTPExtension.Types.Verb.Options,
                HTTPVerb.Trace => Autogenerated.HTTPExtension.Types.Verb.Trace,
                _ => throw new NotImplementedException($"Service invocation with verb '{verb}' is not supported")
            };
        }

        private static Autogenerated.StateOptions.Types.StateConsistency GetStateConsistencyForConsistencyMode(ConsistencyMode consistencyMode)
        {
            if (consistencyMode.Equals(ConsistencyMode.Eventual))
            {
                return Autogenerated.StateOptions.Types.StateConsistency.ConsistencyEventual;
            }

            if (consistencyMode.Equals(ConsistencyMode.Strong))
            {
                return Autogenerated.StateOptions.Types.StateConsistency.ConsistencyStrong;
            }

            throw new ArgumentException($"{consistencyMode} Consistency Mode is not supported.");
        }

        private static Autogenerated.StateOptions.Types.StateConcurrency GetStateConcurrencyForConcurrencyMode(ConcurrencyMode concurrencyMode)
        {
            if (concurrencyMode.Equals(ConcurrencyMode.FirstWrite))
            {
                return Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyFirstWrite;
            }

            if (concurrencyMode.Equals(ConcurrencyMode.LastWrite))
            {
                return Autogenerated.StateOptions.Types.StateConcurrency.ConcurrencyLastWrite;
            }

            throw new ArgumentException($"{concurrencyMode} Concurrency Mode is not supported.");
        }

        private static Autogenerated.StateRetryPolicy.Types.RetryPattern GetRetryPatternForRetryMode(RetryMode retryMode)
        {
            if (retryMode.Equals(RetryMode.Exponential))
            {
                return Autogenerated.StateRetryPolicy.Types.RetryPattern.RetryExponential;
            }

            if (retryMode.Equals(RetryMode.Linear))
            {
                return Autogenerated.StateRetryPolicy.Types.RetryPattern.RetryLinear;
            }

            throw new ArgumentException($"{retryMode} Retry Mode is not supported.");
        }
        #endregion Helper Methods
    }
}


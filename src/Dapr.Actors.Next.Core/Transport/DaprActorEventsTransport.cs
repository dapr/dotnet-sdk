using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Dapr.Actors.Next.Core;
using P = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Actors.Next.Core.Transport;

/// <summary>
/// Opens the generated Dapr actor event stream and maps it to the Core callback transport abstraction.
/// </summary>
public sealed class DaprActorEventsTransport : ISubscribeActorEventsTransport
{
    private readonly Lazy<P.Dapr.DaprClient> client;
    private readonly string? daprApiToken;
    private readonly string? daprGrpcEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprActorEventsTransport"/> class.
    /// </summary>
    public DaprActorEventsTransport(P.Dapr.DaprClient client, string? daprApiToken = null)
        : this(CreateEagerAccessor(client), daprApiToken, daprGrpcEndpoint: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DaprActorEventsTransport"/> class whose Dapr gRPC
    /// client is resolved on first use, so constructing the transport does not eagerly build the transport channel.
    /// </summary>
    public DaprActorEventsTransport(Lazy<P.Dapr.DaprClient> client, string? daprApiToken = null, string? daprGrpcEndpoint = null)
    {
        this.client = client ?? throw new ArgumentNullException(nameof(client));
        this.daprApiToken = daprApiToken;
        this.daprGrpcEndpoint = daprGrpcEndpoint;
    }

    /// <inheritdoc />
    public ValueTask<ISubscribeActorEventsStream> OpenStreamAsync(CancellationToken cancellationToken = default)
    {
        var call = client.Value.SubscribeActorEventsAlpha1(DaprActorGrpcCallOptions.Create(daprApiToken, cancellationToken));
        return ValueTask.FromResult<ISubscribeActorEventsStream>(new Stream(call, daprGrpcEndpoint));
    }

    private static Lazy<P.Dapr.DaprClient> CreateEagerAccessor(P.Dapr.DaprClient client)
    {
        ArgumentNullException.ThrowIfNull(client);
        return new Lazy<P.Dapr.DaprClient>(() => client);
    }

    private sealed class Stream(
        AsyncDuplexStreamingCall<P.SubscribeActorEventsRequestAlpha1, P.SubscribeActorEventsResponseAlpha1> call,
        string? daprGrpcEndpoint) : ISubscribeActorEventsStream
    {
        public async IAsyncEnumerable<SubscribeActorEventsRequest> ReadAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var response in call.ResponseStream.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                var request = ToRuntimeRequest(response);
                if (request is not null)
                {
                    yield return request;
                }
            }
        }

        public async ValueTask WriteAsync(SubscribeActorEventsResponse response, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(response);

            try
            {
                await call.RequestStream.WriteAsync(ToProtoRequest(response), cancellationToken).ConfigureAwait(false);
            }
            catch (RpcException ex) when (LooksLikeHttp2ProtocolMismatch(ex))
            {
                throw CreateProtocolMismatchException(ex, daprGrpcEndpoint);
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await call.RequestStream.CompleteAsync().ConfigureAwait(false);
            }
            finally
            {
                call.Dispose();
            }
        }

        private static SubscribeActorEventsRequest? ToRuntimeRequest(P.SubscribeActorEventsResponseAlpha1 response)
        {
            return response.ResponseTypeCase switch
            {
                P.SubscribeActorEventsResponseAlpha1.ResponseTypeOneofCase.InitialResponse => null,
                P.SubscribeActorEventsResponseAlpha1.ResponseTypeOneofCase.InvokeRequest => ToRuntimeRequest(response.InvokeRequest),
                P.SubscribeActorEventsResponseAlpha1.ResponseTypeOneofCase.ReminderRequest => ToRuntimeRequest(response.ReminderRequest),
                P.SubscribeActorEventsResponseAlpha1.ResponseTypeOneofCase.TimerRequest => ToRuntimeRequest(response.TimerRequest),
                P.SubscribeActorEventsResponseAlpha1.ResponseTypeOneofCase.DeactivateRequest => ToRuntimeRequest(response.DeactivateRequest),
                _ => null,
            };
        }

        private static SubscribeActorEventsRequest ToRuntimeRequest(P.SubscribeActorEventsResponseInvokeRequestAlpha1 request)
        {
            return new SubscribeActorEventsRequest(
                request.Id,
                SubscribeActorEventsFrameKind.Invoke,
                request.ActorType,
                request.ActorId,
                request.Method,
                request.Data.Memory,
                new Dictionary<string, string>(request.Metadata, StringComparer.Ordinal));
        }

        private static SubscribeActorEventsRequest ToRuntimeRequest(P.SubscribeActorEventsResponseReminderRequestAlpha1 request)
        {
            var headers = CreateTimerHeaders(request.DueTime, request.Period, request.Data);
            return new SubscribeActorEventsRequest(
                request.Id,
                SubscribeActorEventsFrameKind.Reminder,
                request.ActorType,
                request.ActorId,
                request.Name,
                request.Data?.Value.Memory ?? ReadOnlyMemory<byte>.Empty,
                headers);
        }

        private static SubscribeActorEventsRequest ToRuntimeRequest(P.SubscribeActorEventsResponseTimerRequestAlpha1 request)
        {
            var headers = CreateTimerHeaders(request.DueTime, request.Period, request.Data);
            return new SubscribeActorEventsRequest(
                request.Id,
                SubscribeActorEventsFrameKind.Timer,
                request.ActorType,
                request.ActorId,
                string.IsNullOrWhiteSpace(request.Callback) ? request.Name : request.Callback,
                request.Data?.Value.Memory ?? ReadOnlyMemory<byte>.Empty,
                headers);
        }

        private static SubscribeActorEventsRequest ToRuntimeRequest(P.SubscribeActorEventsResponseDeactivateRequestAlpha1 request)
        {
            return new SubscribeActorEventsRequest(
                request.Id,
                SubscribeActorEventsFrameKind.Deactivate,
                request.ActorType,
                request.ActorId,
                string.Empty,
                ReadOnlyMemory<byte>.Empty,
                ActorHeaders.Empty);
        }

        private static P.SubscribeActorEventsRequestAlpha1 ToProtoRequest(SubscribeActorEventsResponse response)
        {
            if (response.FailureMessage is not null)
            {
                return new P.SubscribeActorEventsRequestAlpha1
                {
                    RequestFailed = new P.SubscribeActorEventsRequestFailedAlpha1
                    {
                        Id = response.Id,
                        Code = response.FailureCode,
                        Message = response.FailureMessage,
                    },
                };
            }

            return response.Kind switch
            {
                SubscribeActorEventsFrameKind.RegisteredActors => new P.SubscribeActorEventsRequestAlpha1
                {
                    InitialRequest = CreateInitialRequest(response),
                },
                SubscribeActorEventsFrameKind.Invoke => CreateInvokeResponse(response),
                SubscribeActorEventsFrameKind.Reminder => new P.SubscribeActorEventsRequestAlpha1
                {
                    ReminderResponse = new P.SubscribeActorEventsRequestReminderResponseAlpha1 { Id = response.Id, Cancel = response.Cancel },
                },
                SubscribeActorEventsFrameKind.Timer => new P.SubscribeActorEventsRequestAlpha1
                {
                    TimerResponse = new P.SubscribeActorEventsRequestReminderResponseAlpha1 { Id = response.Id, Cancel = response.Cancel },
                },
                SubscribeActorEventsFrameKind.Deactivate => new P.SubscribeActorEventsRequestAlpha1
                {
                    DeactivateResponse = new P.SubscribeActorEventsRequestDeactivateResponseAlpha1 { Id = response.Id },
                },
                _ => throw new InvalidOperationException($"Unsupported actor callback response kind: {response.Kind}."),
            };
        }

        private static bool LooksLikeHttp2ProtocolMismatch(RpcException exception)
        {
            var text = exception.ToString();
            return text.Contains("HTTP/2", StringComparison.OrdinalIgnoreCase)
                && text.Contains("PROTOCOL_ERROR", StringComparison.OrdinalIgnoreCase);
        }

        private static InvalidOperationException CreateProtocolMismatchException(RpcException inner, string? daprGrpcEndpoint)
        {
            var endpoint = string.IsNullOrWhiteSpace(daprGrpcEndpoint) ? "the configured Dapr gRPC endpoint" : daprGrpcEndpoint;
            return new InvalidOperationException(
                $"Failed to write the SubscribeActorEvents stream to daprd at {endpoint}. " +
                "This usually means DAPR_GRPC_ENDPOINT or DAPR_GRPC_PORT points to daprd's HTTP API port, " +
                "an app port, or another non-gRPC endpoint. Actor callbacks use this app-initiated stream; " +
                "current daprd builds may still require a gRPC app channel configured with --app-protocol grpc.",
                inner);
        }

        private static string[] DecodeActorTypes(ReadOnlyMemory<byte> payload)
        {
            var text = System.Text.Encoding.UTF8.GetString(payload.Span);
            return text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }

        private static P.SubscribeActorEventsRequestInitialAlpha1 CreateInitialRequest(SubscribeActorEventsResponse response)
        {
            var request = new P.SubscribeActorEventsRequestInitialAlpha1
            {
                Entities = { DecodeActorTypes(response.Payload) },
            };

            if (response.InitialConfig is not { } config)
            {
                return request;
            }

            var entityConfig = new P.ActorEntityConfig
            {
                ActorIdleTimeout = Duration.FromTimeSpan(config.ActorIdleTimeout),
                DrainOngoingCallTimeout = Duration.FromTimeSpan(config.DrainOngoingCallTimeout),
                DrainRebalancedActors = config.DrainRebalancedActors,
                Reentrancy = new P.ActorReentrancyConfig
                {
                    Enabled = config.EnableReentrancy,
                    MaxStackDepth = config.MaxReentrantDepth,
                },
            };
            entityConfig.Entities.Add(request.Entities);
            request.EntitiesConfig.Add(entityConfig);
            return request;
        }

        private static P.SubscribeActorEventsRequestAlpha1 CreateInvokeResponse(SubscribeActorEventsResponse response)
        {
            var invokeResponse = new P.SubscribeActorEventsRequestInvokeResponseAlpha1
            {
                Id = response.Id,
                Data = ByteString.CopyFrom(response.Payload.Span),
                Error = response.Error,
            };

            foreach (var (key, value) in response.Headers)
            {
                invokeResponse.Metadata[key] = value;
            }

            return new P.SubscribeActorEventsRequestAlpha1 { InvokeResponse = invokeResponse };
        }

        private static IReadOnlyDictionary<string, string> CreateTimerHeaders(string dueTime, string period, Any? data)
        {
            var headers = new Dictionary<string, string>(StringComparer.Ordinal);
            if (!string.IsNullOrWhiteSpace(dueTime))
            {
                headers["dapr-due-time"] = dueTime;
            }

            if (!string.IsNullOrWhiteSpace(period))
            {
                headers["dapr-period"] = period;
            }

            if (!string.IsNullOrWhiteSpace(data?.TypeUrl))
            {
                headers["content-type"] = data.TypeUrl;
            }

            return headers;
        }
    }
}

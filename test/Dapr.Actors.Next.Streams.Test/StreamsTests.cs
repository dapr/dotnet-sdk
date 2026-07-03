using System.Runtime.CompilerServices;
using System.Text;
using Dapr.Actors.Next.Streams;
using Dapr.Actors.Next.Core.Client;
using Dapr.Messaging.PublishSubscribe;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Dapr.Actors.Next.Streams.Test;

public sealed class StreamsTests
{
    private static readonly ActorStreamSubscription Subscription = new("pubsub", "topic", "CartActor", "OnEvent", "cartId");

    [Fact]
    public void Routing_key_reads_cloudevents_attribute_subject_and_content_path()
    {
        var extractor = new ActorStreamRoutingKeyExtractor();
        var byAttribute = Event("""{"cartId":"json-cart"}""", new Dictionary<string, string> { ["cartId"] = "attribute-cart" });
        var bySubject = Event("""{"cartId":"json-cart"}""", new Dictionary<string, string> { ["subject"] = "subject-cart" });
        var byContent = Event("""{"order":{"cartId":"content-cart"}}""");

        Assert.Equal("attribute-cart", extractor.ExtractActorId(Subscription, byAttribute));
        Assert.Equal("subject-cart", extractor.ExtractActorId(Subscription with { RouteBy = "subject" }, bySubject));
        Assert.Equal("content-cart", extractor.ExtractActorId(Subscription with { RouteBy = "data.order.cartId" }, byContent));
        Assert.Equal("42", extractor.ExtractActorId(Subscription with { RouteBy = "cartId" }, Event("""{"cartId":42}""")));
        Assert.Equal("True", extractor.ExtractActorId(Subscription with { RouteBy = "cartId" }, Event("""{"cartId":true}""")));
        Assert.Equal("camel-cart", extractor.ExtractActorId(Subscription with { RouteBy = "CartId" }, Event("""{"cartId":"camel-cart"}""")));
    }

    [Fact]
    public void Routing_key_rejects_empty_missing_and_non_scalar_values()
    {
        var extractor = new ActorStreamRoutingKeyExtractor();

        Assert.Throws<ArgumentException>(() => extractor.ExtractActorId(Subscription, Event("""{"cartId":""}""")));
        Assert.Throws<ArgumentException>(() => extractor.ExtractActorId(Subscription, Event("""{"other":"x"}""")));
        Assert.Throws<ArgumentException>(() => extractor.ExtractActorId(Subscription, Event("""{"cartId":{"nested":"x"}}""")));
        Assert.Throws<ArgumentException>(() => extractor.ExtractActorId(Subscription, new ActorStreamEvent("1", "pubsub", "topic", ReadOnlyMemory<byte>.Empty, new Dictionary<string, string>())));
    }

    [Fact]
    public void Cloudevents_attribute_lookup_is_case_insensitive()
    {
        var evt = Event("""{"cartId":"json"}""", new Dictionary<string, string> { ["TraceParent"] = "trace", ["CartId"] = "cart" });

        Assert.True(evt.TryGetAttribute("traceparent", out var trace));
        Assert.Equal("trace", trace);
        Assert.Equal("trace", evt.TraceParent);
        Assert.Equal("cart", new ActorStreamRoutingKeyExtractor().ExtractActorId(Subscription, evt));
    }

    [Fact]
    public void Topic_message_mapper_preserves_cloudevents_attributes_and_extensions()
    {
        var message = new TopicMessage("id-1", "source", "type", "1.0", "application/json", "topic", "pubsub")
        {
            Data = Encoding.UTF8.GetBytes("""{"cartId":"cart"}"""),
            Path = "matched",
            Extensions = new Dictionary<string, Value>
            {
                ["traceparent"] = Value.ForString("trace"),
                ["subject"] = Value.ForString("cart"),
                ["attempt"] = Value.ForNumber(2),
                ["flag"] = Value.ForBool(true),
                ["empty"] = Value.ForNull(),
                ["object"] = Value.ForStruct(new Struct()),
            },
        };

        var evt = ActorStreamTopicMessageMapper.ToActorStreamEvent(message);

        Assert.Equal("id-1", evt.Id);
        Assert.Equal("source", evt.Attributes["source"]);
        Assert.Equal("matched", evt.Attributes["path"]);
        Assert.Equal("trace", evt.TraceParent);
        Assert.Equal("cart", new ActorStreamRoutingKeyExtractor().ExtractActorId(Subscription with { RouteBy = "subject" }, evt));
        Assert.Equal("2", evt.Attributes["attempt"]);
        Assert.Equal("True", evt.Attributes["flag"]);
        Assert.Equal(string.Empty, evt.Attributes["empty"]);
        Assert.NotEmpty(evt.Attributes["object"]);
    }

    [Fact]
    public async Task Delivery_ack_is_gated_until_forward_invoke_completes()
    {
        var client = new FakeInvocationClient();
        var runner = Runner(client);
        var evt = Event("""{"cartId":"cart-1"}""");

        var action = await runner.ProcessEventAsync(Subscription, evt);

        Assert.Equal(ActorStreamDeliveryAction.Ack, action);
        Assert.Single(client.Invocations);
        Assert.Equal("cart-1", client.Invocations[0].ActorId);
        Assert.Equal("""{"cartId":"cart-1"}""", Encoding.UTF8.GetString(client.Invocations[0].Payload.ToArray()));
    }

    [Fact]
    public async Task Delivery_retries_transient_failure_and_drops_poison_failure()
    {
        var transient = new FakeInvocationClient { Failure = new ActorStreamTransientException("try again") };
        var poison = new FakeInvocationClient { Failure = new ActorStreamPoisonException("bad event") };

        Assert.Equal(ActorStreamDeliveryAction.Retry, await Runner(transient).ProcessEventAsync(Subscription, Event("""{"cartId":"a"}""")));
        Assert.Equal(ActorStreamDeliveryAction.Drop, await Runner(poison).ProcessEventAsync(Subscription, Event("""{"cartId":"b"}""")));
    }

    [Fact]
    public async Task Run_uses_component_single_delivery_and_acknowledges_each_event_once()
    {
        var source = new FakeSubscriptionSource([
            Event("""{"cartId":"one"}"""),
            Event("""{"cartId":"two"}"""),
        ]);
        var client = new FakeInvocationClient();
        var acknowledgements = new List<(string Id, ActorStreamDeliveryAction Action)>();

        await Runner(client).RunAsync(
            Subscription,
            source,
            (evt, action, _) =>
            {
                acknowledgements.Add((evt.Id, action));
                return ValueTask.CompletedTask;
            });

        Assert.Equal(2, source.YieldCount);
        Assert.Equal(["one", "two"], client.Invocations.Select(item => item.ActorId).ToArray());
        Assert.All(acknowledgements, item => Assert.Equal(ActorStreamDeliveryAction.Ack, item.Action));
    }

    [Fact]
    public async Task Traceparent_is_attached_to_forward_invoke_headers()
    {
        var client = new FakeInvocationClient();
        var traceParent = "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-00";

        await Runner(client).ProcessEventAsync(
            Subscription,
            Event("""{"cartId":"cart-1"}""", new Dictionary<string, string> { ["traceparent"] = traceParent }));

        Assert.Equal(traceParent, client.Invocations[0].Headers["traceparent"]);
    }

    [Fact]
    public async Task Dapr_messaging_subscriber_maps_runner_outcome_to_topic_response()
    {
        var client = new FakePublishSubscribeClient();
        var invocation = new FakeInvocationClient();
        var subscriber = new DaprMessagingActorStreamSubscriber(client, Runner(invocation));

        await using var _ = await subscriber.SubscribeAsync(Subscription with
        {
            Metadata = new Dictionary<string, string> { ["consumerID"] = "actors" },
            DeadLetterTopic = "dead",
            MaximumQueuedMessages = 8,
            MessageTimeout = TimeSpan.FromSeconds(5),
        });

        var action = await client.Handler!(
            new TopicMessage("id", "source", "type", "1.0", "application/json", "topic", "pubsub")
            {
                Data = Encoding.UTF8.GetBytes("""{"cartId":"cart-1"}"""),
                Extensions = new Dictionary<string, Value> { ["traceparent"] = Value.ForString("trace") },
            },
            CancellationToken.None);

        Assert.Equal(TopicResponseAction.Success, action);
        Assert.Equal("pubsub", client.Subscriptions[0].Pubsub);
        Assert.Equal("topic", client.Subscriptions[0].Topic);
        Assert.Equal("actors", client.Subscriptions[0].Options.Metadata["consumerID"]);
        Assert.Equal("dead", client.Subscriptions[0].Options.DeadLetterTopic);
        Assert.Equal(8, client.Subscriptions[0].Options.MaximumQueuedMessages);
        Assert.Equal(TimeSpan.FromSeconds(5), client.Subscriptions[0].Options.MessageHandlingPolicy.TimeoutDuration);
        Assert.Equal("trace", invocation.Invocations[0].Headers["traceparent"]);
    }

    [Fact]
    public async Task Dapr_messaging_subscriber_maps_retry_and_drop()
    {
        var retryClient = new FakePublishSubscribeClient();
        var retryInvocation = new FakeInvocationClient { Failure = new ActorStreamTransientException("retry") };
        await using (await new DaprMessagingActorStreamSubscriber(retryClient, Runner(retryInvocation)).SubscribeAsync(Subscription))
        {
            var retry = await retryClient.Handler!(
                new TopicMessage("id", "source", "type", "1.0", "application/json", "topic", "pubsub")
                {
                    Data = Encoding.UTF8.GetBytes("""{"cartId":"cart-1"}"""),
                },
                CancellationToken.None);
            Assert.Equal(TopicResponseAction.Retry, retry);
        }

        var dropClient = new FakePublishSubscribeClient();
        var dropInvocation = new FakeInvocationClient { Failure = new ActorStreamPoisonException("drop") };
        await using (await new DaprMessagingActorStreamSubscriber(dropClient, Runner(dropInvocation)).SubscribeAsync(Subscription))
        {
            var drop = await dropClient.Handler!(
                new TopicMessage("id", "source", "type", "1.0", "application/json", "topic", "pubsub")
                {
                    Data = Encoding.UTF8.GetBytes("""{"cartId":"cart-1"}"""),
                },
                CancellationToken.None);
            Assert.Equal(TopicResponseAction.Drop, drop);
        }
    }

    [Fact]
    public async Task Hosted_service_opens_and_disposes_registered_subscriptions()
    {
        var client = new FakePublishSubscribeClient();
        var invocation = new FakeInvocationClient();
        var registry = new ActorStreamSubscriptionRegistry()
            .Add(Subscription)
            .Add(Subscription with { Topic = "other" });
        var service = new ActorStreamSubscriptionHostedService(
            registry,
            new DaprMessagingActorStreamSubscriber(client, Runner(invocation)),
            NullLogger<ActorStreamSubscriptionHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(["topic", "other"], client.Subscriptions.Select(item => item.Topic).ToArray());
        Assert.All(client.Disposables, disposable => Assert.True(disposable.Disposed));
    }

    [Fact]
    public void Invalid_route_is_poison()
    {
        var classifier = new DefaultActorStreamFailureClassifier();

        Assert.Equal(ActorStreamDeliveryAction.Drop, classifier.Classify(new ArgumentException("bad route")));
        Assert.Equal(ActorStreamDeliveryAction.Drop, classifier.Classify(new Dapr.Actors.Next.Abstractions.Exceptions.InvalidActorEventException("bad event")));
        Assert.Equal(ActorStreamDeliveryAction.Drop, classifier.Classify(new RpcException(new Status(StatusCode.NotFound, "missing actor"))));
        Assert.Equal(ActorStreamDeliveryAction.Retry, classifier.Classify(new TimeoutException()));
        Assert.Equal(ActorStreamDeliveryAction.Retry, classifier.Classify(new OperationCanceledException()));
        Assert.Equal(ActorStreamDeliveryAction.Retry, classifier.Classify(new InvalidOperationException()));
    }

    [Fact]
    public void Service_collection_extension_registers_stream_services()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IActorInvocationClient>(new FakeInvocationClient());
        services.AddSingleton<DaprPublishSubscribeClient>(new FakePublishSubscribeClient());

        services.AddDaprActorStreams();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<ActorStreamSubscriptionRegistry>();
        registry.Add(Subscription);
        Assert.Single(provider.GetRequiredService<IActorStreamSubscriptionRegistry>().Subscriptions);
        Assert.NotNull(provider.GetRequiredService<ActorStreamRoutingKeyExtractor>());
        Assert.NotNull(provider.GetRequiredService<ActorStreamForwarder>());
        Assert.NotNull(provider.GetRequiredService<ActorStreamSubscriptionRunner>());
        Assert.NotNull(provider.GetRequiredService<DaprMessagingActorStreamSubscriber>());
        Assert.IsType<DefaultActorStreamFailureClassifier>(provider.GetRequiredService<IActorStreamFailureClassifier>());
    }

    private static ActorStreamSubscriptionRunner Runner(FakeInvocationClient client) =>
        new(new ActorStreamForwarder(client, new ActorStreamRoutingKeyExtractor()), new DefaultActorStreamFailureClassifier());

    private static ActorStreamEvent Event(string json, IReadOnlyDictionary<string, string>? attributes = null) =>
        new(Guid.NewGuid().ToString("N"), "pubsub", "topic", Encoding.UTF8.GetBytes(json), attributes ?? new Dictionary<string, string>(StringComparer.Ordinal));

    private sealed class FakeInvocationClient : IActorInvocationClient
    {
        public List<Invocation> Invocations { get; } = [];

        public Exception? Failure { get; init; }

        public Task<byte[]?> InvokeAsync(
            string actorType,
            string actorId,
            string methodName,
            ReadOnlyMemory<byte> payload,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(actorType, actorId, methodName, payload.ToArray(), new Dictionary<string, string>(headers, StringComparer.Ordinal)));
            return Failure is null ? Task.FromResult<byte[]?>(null) : Task.FromException<byte[]?>(Failure);
        }
    }

    private sealed class FakeSubscriptionSource(IReadOnlyList<ActorStreamEvent> events) : IActorStreamSubscriptionSource
    {
        public int YieldCount { get; private set; }

        public async IAsyncEnumerable<ActorStreamEvent> SubscribeAsync(
            ActorStreamSubscription subscription,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            foreach (var evt in events)
            {
                cancellationToken.ThrowIfCancellationRequested();
                YieldCount++;
                yield return evt;
                await Task.Yield();
            }
        }
    }

    private sealed record Invocation(
        string ActorType,
        string ActorId,
        string MethodName,
        ReadOnlyMemory<byte> Payload,
        IReadOnlyDictionary<string, string> Headers);

    private sealed class FakePublishSubscribeClient : DaprPublishSubscribeClient
    {
        public FakePublishSubscribeClient()
            : base(null!, new HttpClient())
        {
        }

        public List<(string Pubsub, string Topic, DaprSubscriptionOptions Options)> Subscriptions { get; } = [];

        public List<TrackingAsyncDisposable> Disposables { get; } = [];

        public TopicMessageHandler? Handler { get; private set; }

        public override Task<IAsyncDisposable> SubscribeAsync(
            string pubSubName,
            string topicName,
            DaprSubscriptionOptions options,
            TopicMessageHandler messageHandler,
            CancellationToken cancellationToken = default)
        {
            Subscriptions.Add((pubSubName, topicName, options));
            Handler = messageHandler;
            var disposable = new TrackingAsyncDisposable();
            Disposables.Add(disposable);
            return Task.FromResult<IAsyncDisposable>(disposable);
        }
    }

    private sealed class TrackingAsyncDisposable : IAsyncDisposable
    {
        public bool Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}

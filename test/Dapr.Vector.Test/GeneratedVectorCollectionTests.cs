// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
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

using Dapr.Vector.Extensions;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.DependencyInjection;
using Proto = Dapr.Client.Autogen.Grpc.v1;

namespace Dapr.Vector.Test;

[VectorCollection("generated-store", "books")]
internal partial interface IBookVectors : IDaprVectorCollection<Book>;

public sealed class GeneratedVectorCollectionTests
{
    [Fact]
    public async Task ForwardsEveryBoundOperationAndOptions()
    {
        var ct = TestContext.Current.CancellationToken;
        var invoker = new VectorCallInvoker { Response = new Empty() };
        using var client = DaprVectorClientTests.CreateClient(invoker);
        var services = new ServiceCollection();
        services.AddSingleton(client);
        var builder = new DaprVectorBuilder(services);
        Assert.Same(builder, builder.AddBookVectors());
        using var provider = services.BuildServiceProvider();
        var collection = provider.GetRequiredService<IBookVectors>();
        var metadata = new Dictionary<string, string> { ["setting"] = "value" };
        var wait = new IndexingOptions
        {
            Mode = IndexingMode.WaitForCompletion,
            WaitTimeout = TimeSpan.FromSeconds(5),
            OnWaitTimeout = IndexingWaitTimeoutAction.FailRequest
        };
        await collection.CreateCollectionAsync(3, DistanceMetric.Cosine, metadata, ct);
        var create = Assert.IsType<Proto.CreateCollectionRequestAlpha1>(invoker.Request);
        Assert.Equal((3U, Proto.DistanceMetric.Cosine), (create.Dimensions, create.Metric));
        CheckBinding();
        invoker.Response = new Proto.GetCollectionResponseAlpha1
        {
            Collection = "books", RecordCount = 2, Dimensions = 3, Metric = Proto.DistanceMetric.Cosine
        };
        Assert.Equal(2UL, (await collection.GetCollectionAsync(metadata, ct)).RecordCount);
        CheckBinding();
        invoker.Response = new Proto.UpsertVectorsResponseAlpha1 { Ack = Proto.IndexAck.Queued };
        Assert.Equal(IndexAck.Queued,
            (await collection.UpsertVectorsAsync([new("a", [1, 0, 0], new("Dapr"))], wait, metadata, ct)).Ack);
        var upsert = Assert.IsType<Proto.UpsertVectorsRequestAlpha1>(invoker.Request);
        Assert.Equal(wait.WaitTimeout, upsert.Options.WaitTimeout.ToTimeSpan());
        Assert.Equal("""{"title":"Dapr"}""", upsert.Records[0].Payload.ToStringUtf8());
        CheckBinding();
        invoker.Response = new Proto.GetVectorsResponseAlpha1 { Records = { upsert.Records[0] } };
        Assert.Equal(new Book("Dapr"),
            Assert.Single(await collection.GetVectorsAsync(["a"], true, metadata, ct)).Payload);
        Assert.True(Assert.IsType<Proto.GetVectorsRequestAlpha1>(invoker.Request).IncludeValues);
        CheckBinding();
        var response = new Proto.QueryVectorsResponseAlpha1
        {
            Metric = Proto.DistanceMetric.Cosine,
            Matches = { new Proto.VectorMatch { Record = upsert.Records[0], Score = .75 } }
        };
        invoker.Response = response;
        var query = new VectorQuery { ById = "a", IncludePayload = true, IncludeValues = true, TopK = 1 };
        Assert.Equal(new Book("Dapr"),
            Assert.Single((await collection.QueryVectorsAsync(query, metadata, ct)).Matches).Record.Payload);
        var sentQuery = Assert.IsType<Proto.QueryVectorsRequestAlpha1>(invoker.Request).Query;
        Assert.Equal("a", sentQuery.ById);
        CheckBinding();
        invoker.Response = new Proto.BatchQueryVectorsResponseAlpha1
        {
            Results = { new Proto.BatchQueryResultAlpha1 { Response = response } }
        };
        Assert.NotNull(Assert.Single(await collection.BatchQueryVectorsAsync([query], metadata, ct)).Response);
        Assert.Equal(sentQuery,
            Assert.Single(Assert.IsType<Proto.BatchQueryVectorsRequestAlpha1>(invoker.Request).Queries));
        CheckBinding();
        invoker.Response = new Proto.DeleteVectorsResponseAlpha1 { Ack = Proto.IndexAck.Completed };
        Assert.Equal(IndexAck.Completed, await collection.DeleteVectorsAsync(["a"], wait, metadata, ct));
        Assert.Equal(upsert.Options, Assert.IsType<Proto.DeleteVectorsRequestAlpha1>(invoker.Request).Options);
        CheckBinding();
        invoker.Response = new Empty();
        await collection.DeleteCollectionAsync(metadata, ct);
        CheckBinding();

        void CheckBinding()
        {
            var json = JsonFormatter.Default.Format(Assert.IsAssignableFrom<IMessage>(invoker.Request));
            using var document = System.Text.Json.JsonDocument.Parse(json);
            Assert.Equal("generated-store", document.RootElement.GetProperty("storeName").GetString());
            Assert.Equal("books", document.RootElement.GetProperty("collection").GetString());
            Assert.Equal("value", document.RootElement.GetProperty("metadata").GetProperty("setting").GetString());
            Assert.Equal(ct, invoker.Options.CancellationToken);
        }
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void GeneratedRegistrationFollowsClientLifetimeWithoutOwnership(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDaprVectorClient(lifetime: lifetime).AddBookVectors();
        Assert.Equal(lifetime, Assert.Single(services, d => d.ServiceType == typeof(IBookVectors)).Lifetime);
        using var provider =
            services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();
        var first = firstScope.ServiceProvider.GetRequiredService<IBookVectors>();
        Assert.Equal(lifetime != ServiceLifetime.Transient,
            ReferenceEquals(first, firstScope.ServiceProvider.GetRequiredService<IBookVectors>()));
        Assert.Equal(lifetime == ServiceLifetime.Singleton,
            ReferenceEquals(first, secondScope.ServiceProvider.GetRequiredService<IBookVectors>()));
        Assert.False(first is IDisposable);
    }

    [Fact]
    public void RegistrationRequiresBuilderAndClient()
    {
        Assert.Throws<ArgumentNullException>(() => IBookVectorsVectorCollectionExtensions.AddBookVectors(null!));
        Assert.Throws<InvalidOperationException>(() => new DaprVectorBuilder(new ServiceCollection()).AddBookVectors());
    }
}

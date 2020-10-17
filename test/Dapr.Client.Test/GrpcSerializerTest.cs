// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

namespace Dapr.Client.Test
{
    using Dapr.Client.Autogen.Test.Grpc.v1;
    using FluentAssertions;
    using Xunit;

    public class TypeConvertersTest
    {
        private readonly GrpcSerializer serializer;

        public TypeConvertersTest()
        {
            serializer = new GrpcSerializer();
        }

        [Fact]
        public void AnyConversion_GRPC_Pack_Unpack()
        {
            var testRun = new TestRun();
            testRun.Tests.Add(new TestCase() { Name = "test1" });
            testRun.Tests.Add(new TestCase() { Name = "test2" });
            testRun.Tests.Add(new TestCase() { Name = "test3" });

            var any = serializer.ToAny(testRun);
            var type = serializer.FromAny<TestRun>(any);

            type.Should().BeEquivalentTo(testRun);
            any.TypeUrl.Should().Be("type.googleapis.com/TestRun");
            type.Tests.Count.Should().Be(3);
            type.Tests[0].Name.Should().Be("test1");
            type.Tests[1].Name.Should().Be("test2");
            type.Tests[2].Name.Should().Be("test3");
        }

        [Fact]
        public void AnyConversion_JSON_Serialization_Deserialization()
        {
            var response = new Response()
            {
                Name = "test"
            };

            var any = serializer.ToAny(response);
            var type = serializer.FromAny<Response>(any);

            type.Should().BeEquivalentTo(response);
            any.TypeUrl.Should().Be("Dapr.Client.Test.TypeConvertersTest+Response");
            type.Name.Should().Be("test");
        }

        private class Response
        {
            public string Name { get; set; }
        }
    }
}

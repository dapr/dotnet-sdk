using System;
using System.Reflection;
using Xunit;
using Xunit.Sdk;

namespace Dapr.Shared.Test
{
    public class DaprDefaultsTest
    {
        private const string defaultDaprHost = "http://localhost";
        private const string notDefaultDaprHost = "http://1.1.1.1";
        private const string badDaprHost = "bad URL";

        private const string defaultDaprHttpPort = "3500";
        private const string defaultDaprHttpEndpoint = defaultDaprHost + ":" + defaultDaprHttpPort;
        private const string notDefaultDaprHttpPort = "8888";
        private const string badDaprHttpPort = "bad http port";

        private const string defaultDaprGrpcPort = "50001";
        private const string defaultDaprGrpcEndpoint = defaultDaprHost + ":" + defaultDaprGrpcPort;
        private const string notDefaultDaprGrpcPort = "88888";
        private const string badDaprGrpcPort = "bad grpc port";

        public DaprDefaultsTest()
        {
            SetNonPublicStaticFieldValue(typeof(DaprDefaults), "host", null );
            SetNonPublicStaticFieldValue(typeof(DaprDefaults), "httpEndpoint", null);
            SetNonPublicStaticFieldValue(typeof(DaprDefaults), "grpcEndpoint", null);
        }

        [Theory]
        [InlineData("", "", defaultDaprHttpEndpoint)]
        [InlineData(null, null, defaultDaprHttpEndpoint)]
        [InlineData(null, "", defaultDaprHttpEndpoint)]
        [InlineData("", null, defaultDaprHttpEndpoint)]
        [InlineData(notDefaultDaprHost, null, notDefaultDaprHost + ":" + defaultDaprHttpPort)]
        [InlineData("", notDefaultDaprHttpPort, defaultDaprHost + ":" + notDefaultDaprHttpPort)]
        [InlineData(notDefaultDaprHost, notDefaultDaprHttpPort, notDefaultDaprHost  + ":" + notDefaultDaprHttpPort)]
        [InlineData(notDefaultDaprHost, badDaprHttpPort, notDefaultDaprHost + ":" + badDaprHttpPort)]
        [InlineData(badDaprHost, notDefaultDaprHttpPort, badDaprHost + ":" + notDefaultDaprHttpPort)]
        public void GetDefaultHttpEndpoint(string hostEnvironmentVar, string portEnvironmentVar, string expectedResult)
        {
            Environment.SetEnvironmentVariable("DAPR_HOST", hostEnvironmentVar);
            Environment.SetEnvironmentVariable("DAPR_HTTP_PORT", portEnvironmentVar);

            var defaultHttpEndpoint = DaprDefaults.GetDefaultHttpEndpoint();

            Assert.Equal(expectedResult, defaultHttpEndpoint);
        }

        [Theory]
        [InlineData("", "", defaultDaprGrpcEndpoint)]
        [InlineData(null, null, defaultDaprGrpcEndpoint)]
        [InlineData(null, "", defaultDaprGrpcEndpoint)]
        [InlineData("", null, defaultDaprGrpcEndpoint)]
        [InlineData(notDefaultDaprHost, null, notDefaultDaprHost + ":" + defaultDaprGrpcPort)]
        [InlineData("", notDefaultDaprGrpcPort, defaultDaprHost + ":" + notDefaultDaprGrpcPort)]
        [InlineData(notDefaultDaprHost, notDefaultDaprGrpcPort, notDefaultDaprHost + ":" + notDefaultDaprGrpcPort)]
        [InlineData(notDefaultDaprHost, badDaprGrpcPort, notDefaultDaprHost + ":" + badDaprGrpcPort)]
        [InlineData(badDaprHost, notDefaultDaprGrpcPort, badDaprHost + ":" + notDefaultDaprGrpcPort)]
        public void GetDefaultGrpcEndpoint(string hostEnvironmentVar, string portEnvironmentVar, string expectedResult)
        {
            Environment.SetEnvironmentVariable("DAPR_HOST", hostEnvironmentVar);
            Environment.SetEnvironmentVariable("DAPR_GRPC_PORT", portEnvironmentVar);

            var defaultGrpcEndpoint = DaprDefaults.GetDefaultGrpcEndpoint();

            Assert.Equal(expectedResult, defaultGrpcEndpoint);
        }

        private void SetNonPublicStaticFieldValue(Type type, string fieldName, object valueToSet) 
        {
            var field = type.GetField(fieldName, BindingFlags.Static | BindingFlags.NonPublic);
            field?.SetValue(null, valueToSet);
        }
    }
}

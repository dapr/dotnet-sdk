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

using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Common.Options;
using Dapr.Testcontainers.Containers.Dapr;

namespace Dapr.Testcontainers.Test.Common;

public sealed class DaprdContainerTests
{
    [Theory]
    [InlineData(null, "host.docker.internal", "http")]
    [InlineData("my-app", "my-app", "http")]
    [InlineData("my-grpc-app", "my-grpc-app", "grpc")]
    public void BuildCommand_ShouldConfigureAppChannel(string? address, string expectedAddress, string protocol)
    {
        var options = new DaprRuntimeOptions { AppPort = 9090 }.WithAppProtocol(protocol);
        if (address is not null)
        {
            options.WithAppChannelAddress(address);
        }

        var command = DaprdContainer.BuildCommand("test-app", "/components", options);

        Assert.Equal(
            [
                "/daprd",
                "-app-id", "test-app",
                "-dapr-http-port", "3500",
                "-dapr-grpc-port", "50001",
                "-log-level", "info",
                "-resources-path", "/components",
                "-app-port", "9090",
                "-app-channel-address", expectedAddress,
                "-app-protocol", protocol,
                "-placement-host-address", "",
                "-scheduler-host-address", ""
            ],
            command);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void BuildCommand_ShouldOmitAppChannelWhenAppPortIsNotPositive(int appPort)
    {
        var options = new DaprRuntimeOptions { AppPort = appPort }.WithAppChannelAddress("my-app");

        var command = DaprdContainer.BuildCommand("test-app", "/components", options);

        Assert.DoesNotContain("-app-port", command);
        Assert.DoesNotContain("-app-channel-address", command);
        Assert.DoesNotContain("my-app", command);
        Assert.DoesNotContain("-app-protocol", command);
    }

    [Fact]
    public void BuildCommand_ShouldPreserveConfigurationAndServiceAddresses()
    {
        var options = new DaprRuntimeOptions { AppPort = 0 }.WithLogLevel(DaprLogLevel.Warn);

        var command = DaprdContainer.BuildCommand(
            "test-app",
            "/components",
            options,
            new HostPortPair("placement", 50005),
            new HostPortPair("scheduler", 50006),
            "/components/config.yaml");

        Assert.Equal(
            [
                "/daprd",
                "-app-id", "test-app",
                "-dapr-http-port", "3500",
                "-dapr-grpc-port", "50001",
                "-log-level", "warn",
                "-resources-path", "/components",
                "-config", "/components/config.yaml",
                "-placement-host-address", "placement:50005",
                "-scheduler-host-address", "scheduler:50006"
            ],
            command);
    }
}

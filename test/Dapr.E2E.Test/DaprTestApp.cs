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

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.Versioning;
using Xunit.Abstractions;
using static System.IO.Path;

namespace Dapr.E2E.Test;

public class DaprTestApp
{
    static string daprBinaryName = "dapr";
    private string appId;
    private readonly string[] outputToMatchOnStart = new string[] { "dapr initialized. Status: Running.", };
    private readonly string[] outputToMatchOnStop = new string[] { "app stopped successfully", "failed to stop app id", };

    private ITestOutputHelper testOutput;

    public DaprTestApp(ITestOutputHelper output, string appId)
    {
        this.appId = appId;
        this.testOutput = output;
    }

    public string AppId => this.appId;

    public (string httpEndpoint, string grpcEndpoint) Start(DaprRunConfiguration configuration)
    {
        var (appPort, httpPort, grpcPort, metricsPort) = GetFreePorts();

        var resourcesPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "components");
        var configPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "configuration", "featureconfig.yaml");
        var arguments = new List<string>()
        {
            // `dapr run` args
            "run",
            "--app-id", configuration.AppId,
            "--dapr-http-port", httpPort.ToString(CultureInfo.InvariantCulture),
            "--dapr-grpc-port", grpcPort.ToString(CultureInfo.InvariantCulture),
            "--metrics-port", metricsPort.ToString(CultureInfo.InvariantCulture),
            "--resources-path", resourcesPath,
            "--config", configPath,
            "--log-level", "debug",
            "--max-body-size", "8Mi"
        };

        if (configuration.UseAppPort)
        {
            arguments.AddRange(new[] { "--app-port", appPort.ToString(CultureInfo.InvariantCulture), });
        }

        if (!string.IsNullOrEmpty(configuration.AppProtocol))
        {
            arguments.AddRange(new[] { "--app-protocol", configuration.AppProtocol });
        }

        arguments.AddRange(new[]
        {
            // separator
            "--",

            // `dotnet run` args
            "dotnet", "run",
            "--project", configuration.TargetProject,
            "--framework", GetTargetFrameworkName(),
        });

        if (configuration.UseAppPort)
        {
            // The first argument is the port, if the application needs it.
            arguments.AddRange(new[] { "--", $"{appPort.ToString(CultureInfo.InvariantCulture)}" });
            arguments.AddRange(new[] { "--urls", $"http://localhost:{appPort.ToString(CultureInfo.InvariantCulture)}", });
        }

        if (configuration.AppJsonSerialization)
        {
            arguments.AddRange(new[] { "--json-serialization" });
        }

        // TODO: we don't do any quoting right now because our paths are guaranteed not to contain spaces
        var daprStart = new DaprCommand(this.testOutput)
        {
            DaprBinaryName = DaprTestApp.daprBinaryName,
            Command = string.Join(" ", arguments),
            OutputToMatch = outputToMatchOnStart,
            Timeout = TimeSpan.FromSeconds(30),
            EnvironmentVariables = new Dictionary<string, string>
            {
                { "APP_API_TOKEN", "abcdefg" }
            }
        };

        daprStart.Run();

        testOutput.WriteLine($"Dapr app: {appId} started successfully");
        var httpEndpoint = $"http://localhost:{httpPort}";
        var grpcEndpoint = $"http://localhost:{grpcPort}";
        return (httpEndpoint, grpcEndpoint);
    }

    public void Stop()
    {
        var daprStopCommand = $" stop --app-id {appId}";
        var daprStop = new DaprCommand(this.testOutput)
        {
            DaprBinaryName = DaprTestApp.daprBinaryName,
            Command = daprStopCommand,
            OutputToMatch = outputToMatchOnStop,
            Timeout = TimeSpan.FromSeconds(30),
        };
        daprStop.Run();
        testOutput.WriteLine($"Dapr app: {appId} stopped successfully");
    }

    private static string GetTargetFrameworkName()
    {
        var targetFrameworkName = ((TargetFrameworkAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(TargetFrameworkAttribute), false).FirstOrDefault()).FrameworkName;

        return targetFrameworkName switch
        {
            ".NETCoreApp,Version=v6.0" => "net6",
            ".NETCoreApp,Version=v7.0" => "net7",
            ".NETCoreApp,Version=v8.0" => "net8",
            ".NETCoreApp,Version=v9.0" => "net9",
            _ => throw new InvalidOperationException($"Unsupported target framework: {targetFrameworkName}")
        };
    }

    private static (int, int, int, int) GetFreePorts()
    {
        const int NUM_LISTENERS = 4;
        IPAddress ip = IPAddress.Loopback;
        var listeners = new TcpListener[NUM_LISTENERS];
        var ports = new int[NUM_LISTENERS];
        // Note: Starting only one listener at a time might end up returning the
        // same port each time.
        for (int i = 0; i < NUM_LISTENERS; i++)
        {
            listeners[i] = new TcpListener(ip, 0);
            listeners[i].Start();
            ports[i] = ((IPEndPoint)listeners[i].LocalEndpoint).Port;
        }

        for (int i = 0; i < NUM_LISTENERS; i++)
        {
            listeners[i].Stop();
        }
        return (ports[0], ports[1], ports[2], ports[3]);
    }
}
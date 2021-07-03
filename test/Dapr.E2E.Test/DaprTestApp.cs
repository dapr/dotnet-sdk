// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using Xunit.Abstractions;
using static System.IO.Path;
using System.Runtime.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Dapr.E2E.Test
{
    public class DaprTestApp
    {
        static string daprBinaryName = "dapr";
        private string appId;
        private bool useAppPort;
        private readonly string[] outputToMatchOnStart = new string[]{ "dapr initialized. Status: Running.", };
        private readonly string[] outputToMatchOnStop = new string[]{ "app stopped successfully", "failed to stop app id", };

        private ITestOutputHelper testOutput;

        public DaprTestApp(ITestOutputHelper output, string appId, bool useAppPort = false)
        {
            this.appId = appId;
            this.useAppPort = useAppPort;
            this.testOutput = output;
        }

        public string AppId => this.appId;

        public (string httpEndpoint, string grpcEndpoint) Start()
        {
            var (appPort, httpPort, grpcPort, metricsPort) = GetFreePorts();

            var componentsPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "components");
            var projectPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test.App", "Dapr.E2E.Test.App.csproj");
            var configPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "configuration", "featureconfig.yaml");
            var arguments = new List<string>()
            {
                // `dapr run` args
                "run",
                "--app-id", appId,
                "--dapr-http-port", httpPort.ToString(CultureInfo.InvariantCulture),
                "--dapr-grpc-port", grpcPort.ToString(CultureInfo.InvariantCulture),
                "--metrics-port", metricsPort.ToString(CultureInfo.InvariantCulture),
                "--components-path", componentsPath,
                "--config", configPath,
                "--log-level", "debug",
                
            };

            if (this.useAppPort)
            {
                arguments.AddRange(new[]{ "--app-port", appPort.ToString(CultureInfo.InvariantCulture), });
            }

            arguments.AddRange(new[]
            {
                // separator
                "--",

                // `dotnet run` args
                "dotnet", "run",
                "--project", projectPath,
                "--framework", GetTargetFrameworkName(),
            });

            if (this.useAppPort)
            {
                arguments.AddRange(new[]{ "--urls", $"http://localhost:{appPort.ToString(CultureInfo.InvariantCulture)}", });
            }

            // TODO: we don't do any quoting right now because our paths are guaranteed not to contain spaces
            var daprStart = new DaprCommand(this.testOutput)
            {
                DaprBinaryName = DaprTestApp.daprBinaryName,
                Command = string.Join(" ", arguments),
                OutputToMatch = outputToMatchOnStart,
                Timeout = TimeSpan.FromSeconds(30),
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
            string frameworkMoniker;
            frameworkMoniker = targetFrameworkName == ".NETCoreApp,Version=v3.1" ? "netcoreapp3.1" : "net5";
            return frameworkMoniker;
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
}

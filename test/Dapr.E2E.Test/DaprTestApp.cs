// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using Xunit.Abstractions;
using static System.IO.Path;
using System.Runtime.Versioning;

namespace Dapr.E2E.Test
{
    public class DaprTestApp
    {
        static string daprBinaryName = "dapr";
        private string appId;
        private bool useAppPort;
        const string outputToMatchOnStart = "dapr initialized. Status: Running.";
        const string outputToMatchOnStop = "app stopped successfully";

        private ITestOutputHelper testOutput;

        public DaprTestApp(ITestOutputHelper output, string appId, bool useAppPort = false)
        {
            this.appId = appId;
            this.useAppPort = useAppPort;
            this.testOutput = output;
        }

        public (string, string) Start()
        {
            var (appPort, httpPort, grpcPort, metricsPort) = GetFreePorts();
            var componentsPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "components");
            var daprStartCommand = $" run --app-id {appId} --dapr-http-port {httpPort} --dapr-grpc-port {grpcPort} --metrics-port {metricsPort} --components-path {componentsPath}";
            var projectPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test.App", "Dapr.E2E.Test.App.csproj");
            var daprDotnetCommand = $" -- dotnet run --project {projectPath} --framework {GetTargetFrameworkName()}";
            if (this.useAppPort)
            {
                daprStartCommand += $" --app-port {appPort}";
                daprDotnetCommand += $" --urls http://localhost:{appPort}";
            }
            daprStartCommand += daprDotnetCommand;

            var daprStart = new DaprCommand()
            {
                DaprBinaryName = DaprTestApp.daprBinaryName,
                Command = daprStartCommand,
                OutputToMatch = outputToMatchOnStart,
                Timeout = 10000
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
            var daprStop = new DaprCommand()
            {
                DaprBinaryName = DaprTestApp.daprBinaryName,
                Command = daprStopCommand,
                OutputToMatch = outputToMatchOnStop,
                Timeout = 10000
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

// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using static System.IO.Path;

namespace Dapr.E2E.Test
{
    public class DaprApp
    {
        static string shellExeName = SetShell();
        private string appId;
        private int appPort;
        const string outputToMatchOnStart = "You're up and running";
        const string outputToMatchOnStop = "app stopped successfully";

        private static string SetShell()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return "cmd.exe";
            }
            return "/bin/bash";
        }

        public DaprApp(string appId, int appPort = 0)
        {
            this.appId = appId;
            this.appPort = appPort;
        }

        public (string, string) Start()
        {
            var (httpPort, grpcPort, metricsPort) = GetFreePorts();
            var componentsPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test", "components");
            var daprStartCommand = $"dapr run --app-id {appId} --dapr-http-port {httpPort} --dapr-grpc-port {grpcPort} --metrics-port {metricsPort} --components-path {componentsPath}";
            if (this.appPort != 0)
            {
                daprStartCommand += $" --app-port {appPort}";
            }
            var projectPath = Combine(".", "..", "..", "..", "..", "..", "test", "Dapr.E2E.Test.App", "Dapr.E2E.Test.App.csproj");
            daprStartCommand += $" -- dotnet run --project {projectPath}";
            var daprStart = new ShellCommand()
            {
                ShellExeName = DaprApp.shellExeName,
                Command = daprStartCommand,
                OutputToMatch = outputToMatchOnStart,
                Timeout = 5000
            };
            daprStart.Run();
            var httpEndpoint = $"http://localhost:{httpPort}";
            var grpcEndpoint = $"http://localhost:{grpcPort}";
            return (httpEndpoint, grpcEndpoint);
        }

        public void Stop()
        {
            var daprStopCommand = $"dapr stop --app-id {appId}";
            var daprStop = new ShellCommand()
            {
                ShellExeName = DaprApp.shellExeName,
                Command = daprStopCommand,
                OutputToMatch = outputToMatchOnStop,
                Timeout = 10000
            };
            daprStop.Run();
        }

        private static (int, int, int) GetFreePorts()
        {
            IPAddress ip = IPAddress.Loopback;
            var listeners = new TcpListener[3];
            var ports = new int[3];
            // Note: Starting only one listener at a time might end up returning the
            // same port each time.
            for (int i = 0; i < 3; i++)
            {
                listeners[i] = new TcpListener(ip, 0);
                listeners[i].Start();
                ports[i] = ((IPEndPoint)listeners[i].LocalEndpoint).Port;
            }

            for (int i = 0; i < 3; i++)
            {
                listeners[i].Stop();
            }
            return (ports[0], ports[1], ports[2]);
        }

    }
}

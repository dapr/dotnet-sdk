// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
// ------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Linq;
using System.Runtime.InteropServices;

namespace Dapr.E2E.Test
{
    public class DaprApp
    {
        // private string pathSeparator;
        static string shellExeName = SetShell();
        private string appId;
        private int appPort;
        const string outputToMatchOnStart = "You're up and running";
        const string outputToMatchOnStop = "app stopped successfully";

        static char pathSeparator = System.IO.Path.DirectorySeparatorChar;

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
            var componentsPath = string.Format($"..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}test{DaprApp.pathSeparator}Dapr.E2E.Test{DaprApp.pathSeparator}components");
            var daprStartCommand = string.Format($"dapr run --app-id {appId} --dapr-http-port {httpPort} --dapr-grpc-port {grpcPort} --metrics-port {metricsPort} --components-path .{DaprApp.pathSeparator}{componentsPath}");
            if(this.appPort != 0)
            {
                daprStartCommand += string.Format($" --app-port {appPort}");
            }
            var projectPath = string.Format($"..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}..{DaprApp.pathSeparator}test{DaprApp.pathSeparator}Dapr.E2E.Test.App{DaprApp.pathSeparator}Dapr.E2E.Test.App.csproj");
            daprStartCommand += string.Format($" -- dotnet run --project {projectPath}");
            var daprStart = new ShellCommand()
            {
                ShellExeName = DaprApp.shellExeName,
                Command = daprStartCommand,
                OutputToMatch = outputToMatchOnStart,
                Timeout = 5000
            };
            daprStart.Run();
            var httpEndpoint = string.Format($"http://localhost:{httpPort}");
            var grpcEndpoint = string.Format($"http://localhost:{grpcPort}");
            Console.WriteLine("Dapr App started successfully");
            return (httpEndpoint, grpcEndpoint);
        }

        public void Stop()
        {
            var daprStopCommand = string.Format($"dapr stop --app-id {appId}");
            var daprStop = new ShellCommand()
            {
                ShellExeName = DaprApp.shellExeName,
                Command = daprStopCommand,
                OutputToMatch = outputToMatchOnStop,
                Timeout = 10000
            };
            daprStop.Run();
            Console.WriteLine("Dapr App stopped successfully");
        }

        private static (int, int, int) GetFreePorts()
        {
            IPAddress ip = IPAddress.Loopback;
            var listeners = new TcpListener[3];
            var ports = new int[3];
            foreach (int i in Enumerable.Range(0, 3))
            {
                listeners[i] = new TcpListener(ip, 0);
                listeners[i].Start();
                ports[i] = ((IPEndPoint)listeners[i].LocalEndpoint).Port;
            }

            foreach (int i in Enumerable.Range(0, 3))
            {
                listeners[i].Stop();
            }
            return (ports[0], ports[1], ports[2]);
        }

    }
}

// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//  ------------------------------------------------------------------------

using System;
using System.Formats.Tar;
using System.Net;
using System.Net.Sockets;

namespace Dapr.TestContainers.Common;

/// <summary>
/// Provides port-related utilities.
/// </summary>
public static class PortUtilities
{
    /// <summary>
    /// Finds and reserves a port that's available to use.
    /// </summary>
    /// <returns>The available port number and a disposable that keeps the port busy.</returns>
    public static (int port, IDisposable listener) ReserveNextAvailablePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        return (port, listener); // We return the listener so it keeps the port "taken"
    }
}

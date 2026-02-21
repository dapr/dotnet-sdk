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
using System.Net;
using System.Net.Sockets;

namespace Dapr.Testcontainers.Common;

/// <summary>
/// Represents a temporary reservation for a TCP port.
/// </summary>
public sealed class PortReservation : IDisposable
{
    private Socket? _socket;

    internal PortReservation(Socket socket)
    {
        _socket = socket;
        Port = ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    /// <summary>
    /// The reserved port number.
    /// </summary>
    public int Port { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        _socket?.Dispose();
        _socket = null;
    }
}

/// <summary>
/// Provides port-related utilities.
/// </summary>
public static class PortUtilities
{
    /// <summary>
    /// Reserves an available TCP port until the returned reservation is disposed.
    /// </summary>
    /// <returns>A <see cref="PortReservation"/> representing the reserved port.</returns>
    public static PortReservation ReserveTcpPort()
    {
        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
        socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        return new PortReservation(socket);
    }

    /// <summary>
    /// Gets an available TCP port from the OS. This is a best-effort snapshot
    /// and does not reserve the port for later use.
    /// </summary>
    /// <returns>The available port number.</returns>
    public static int GetAvailablePort()
    {
        using var reservation = ReserveTcpPort();
        return reservation.Port;
    }
}
